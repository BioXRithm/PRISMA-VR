using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using TMPro;
using UnityEngine.UI;

public class InsecureCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // Siempre retorna true, aceptando cualquier certificado (incluso si es nulo o inválido)
        return true; 
    }
}


public class LogSender : MonoBehaviour
{
	
	[Header("UI Reference")]
    public TextMeshProUGUI pairingCodeDisplay;
    public TextMeshProUGUI urlDisplay;
	
    private const string API_URL = "https://volterraapi.onrender.com";
    private static string sessionId;
    private static string deviceId;


    void Awake()
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            // Obtener o generar un ID único para este dispositivo
            deviceId = GetOrCreateDeviceId();
            
            // Generar session ID único combinando device + timestamp + random
            sessionId = GenerateSessionId();
            Debug.Log($"🎮 Nueva sesión iniciada: {sessionId}");
            Debug.Log($"📱 Dispositivo: {deviceId}");
        }
    }
	
	void Start()
    {
        if (pairingCodeDisplay != null) pairingCodeDisplay.text = "Generando...";
        
        // Al iniciar la app, pedimos el código automáticamente
        RequestPairingCode((code) => {
            if (pairingCodeDisplay != null) {
                pairingCodeDisplay.text = code;
                
            }
            if (urlDisplay != null) {
                urlDisplay.text = "https://volterraapi.onrender.com/download-logs/" + code;
                
            } 
        });
    }
	
	public void RequestPairingCode(Action<string> onCodeReceived)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "device_id", deviceId },
            { "session_id", sessionId }
        };
        StartCoroutine(PostPairingRequest(data, onCodeReceived));
    }
	
	private IEnumerator PostPairingRequest(Dictionary<string, object> data, Action<string> callback)
    {
        string url = $"{API_URL}/download-code";
        string jsonData = JsonConvert.SerializeObject(data);

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.certificateHandler = new InsecureCertificateHandler();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(webRequest.downloadHandler.text);
                callback?.Invoke(response["pairing_code"]);
            }
            else
            {
                if (pairingCodeDisplay != null) pairingCodeDisplay.text = "Error";
                //Debug.LogError("Error API: " + webRequest.error);
            }
        }
    }

    /// <summary>
    /// Obtiene un ID único y persistente para este dispositivo
    /// </summary>
    private string GetOrCreateDeviceId()
    {
        // Intentar obtener un device_id previamente guardado
        string savedDeviceId = PlayerPrefs.GetString("device_id", "");
        
        if (string.IsNullOrEmpty(savedDeviceId))
        {
            // Unity proporciona un identificador único del dispositivo
            // SystemInfo.deviceUniqueIdentifier es único por dispositivo
            savedDeviceId = SystemInfo.deviceUniqueIdentifier;
            
            // Si no está disponible (raro), generar uno
            if (string.IsNullOrEmpty(savedDeviceId))
            {
                savedDeviceId = Guid.NewGuid().ToString("N");
            }
            
            // Guardar para futuras sesiones
            PlayerPrefs.SetString("device_id", savedDeviceId);
            PlayerPrefs.Save();
        }
        
        return savedDeviceId;
    }

    /// <summary>
    /// Genera un Session ID GARANTIZADO único
    /// Formato: {device_short}_{timestamp}_{random}
    /// </summary>
    private string GenerateSessionId()
    {
        // Usar los primeros 8 caracteres del device ID
        string deviceShort = deviceId.Substring(0, Math.Min(8, deviceId.Length));
        
        // Timestamp con milisegundos
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        
        // Añadir componente aleatorio adicional por si acaso
        string random = Guid.NewGuid().ToString("N").Substring(0, 6);
        
        return $"{deviceShort}_{timestamp}_{random}";
    }

    /// <summary>
    /// Envía una entrada de log flexible a la API de FastAPI.
    /// Utiliza un Dictionary<string, object> para manejar datos flexibles (clave-valor).
    /// </summary>
    /// <param name="logData">Diccionario con los datos del log (ej: "level", "message", "user_id").</param>
    public void SendLog(Dictionary<string, object> logData)
    {
        logData["session_id"] = sessionId;
        logData["device_id"] = deviceId;
        StartCoroutine(PostRequest(logData));
    }

    private IEnumerator PostRequest(Dictionary<string, object> logData)
    {
        // 1. Serializar el Dictionary<string, object> a una cadena JSON
        string url = $"{API_URL}/log_entry";
        //Debug.LogError("----------- URL ---------------" + url);
        string jsonData = JsonConvert.SerializeObject(logData);
        //Debug.LogError("JSON ENVIADO" + jsonData);
        // 2. Crear la petición UnityWebRequest
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            // Convertir la cadena JSON a bytes
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            webRequest.certificateHandler = new InsecureCertificateHandler();
            // Adjuntar los bytes al UploadHandler
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            
            // El DownloadHandlerText es necesario para recibir la respuesta del servidor
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            
            // 3. Establecer el Content-Type (CRUCIAL para FastAPI)
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // 4. Enviar la petición y esperar la respuesta
            yield return webRequest.SendWebRequest();

            // 5. Manejo de errores y respuesta
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ ERROR al enviar log a FastAPI. HTTP Code: {webRequest.responseCode}. Error: {webRequest.error}");
            }
            else
            {
                Debug.Log($"✅ Log enviado con éxito. Respuesta del servidor: {webRequest.downloadHandler.text}");
            }
        }
    }

    public void sendPopulationUpdate(int preys, int predators, int invaders = -2) 
    {
        Dictionary<string, object> logData = new Dictionary<string, object>
        {
            {"level", "INFO"},
            {"event_type", "Iteration"},
            {"timestamp_utc", System.DateTime.UtcNow.ToString("o")},
            {"unity_scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name},
            {"preys_count", preys}, 
            {"predators_count", predators},
        };

        // Solo añadimos "invaders_count" si realmente hay invasores (opcional)
        if (invaders >= 0) {
            logData.Add("invaders_count", invaders);
        }

        this.SendLog(logData);
        Debug.Log("Log de punto añadido enviado a la API.");
    }
}