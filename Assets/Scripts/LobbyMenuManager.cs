using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using UnityEngine.UI;

public class LobbyMenuManager : MonoBehaviour
{

    [Header("UI References - Lobby List")]
    [Tooltip("Container con los 5 slots fijos para las filas de lobby")]
    public Transform[] lobbySlots = new Transform[3];
    
    [Tooltip("El prefab LobbyRow de /Assets/Prefabs/")]
    public GameObject lobbyRowPrefab;
    
    [Header("UI References - Pagination")]
    [Tooltip("Botón Search")]
    public Toggle searchButton;

    [Tooltip("Botón Previous")]
    public Toggle previousButton;
    
    [Tooltip("Botón Next")]
    public Toggle nextButton;

    [Header("UI Elements - Toggles")]
    public Toggle resetToggle;
    public Toggle updateToggle;
    public Toggle startToggle;
    public Toggle createHostToggle;
    public Toggle disconnectToggle;
    public Toggle addInvaderToggle;
    public Toggle removeInvaderToggle;
    
    [Tooltip("Texto que muestra 'Página X/Y'")]
    public TMP_Text pageInfoText;
    
    [Header("UI References - Status")]
    [Tooltip("Texto para mostrar el estado actual")]
    public TMP_Text statusText;
    public TMP_Text connectLabel;
    
    [Header("Settings")]
    [Tooltip("Intervalo de refresco automático en segundos")]
    public float autoRefreshInterval = 5f;
    
    [Tooltip("Activar para refrescar automáticamente")]
    public bool autoRefresh = true;
    
    [Tooltip("Lobbies por página")]
    public int lobbiesPerPage = 5;
    
    private const string RELAY_CODE_KEY = "relayCode";
    private bool servicesInitialized = false;
    private float nextRefreshTime = 0f;
    private bool isRefreshing = false;
    
    private List<Lobby> allLobbies = new List<Lobby>();
    private int currentPage = 0;
    private int totalPages = 0;

    private Lobby joinedLobby;

    async void Start()
    {

        if (searchButton != null)
        {
            searchButton.onValueChanged.AddListener(OnSearchToggled);
        }

        if (disconnectToggle != null)
        {
            disconnectToggle.onValueChanged.AddListener(OnDisconnectToggled);
        }
        //SetupPaginationButtons();
        await InitializeServices();
    }

    void Update()
    {
        if (autoRefresh && servicesInitialized && !isRefreshing)
        {
            if (Time.time >= nextRefreshTime)
            {
                RefreshLobbyList();
                nextRefreshTime = Time.time + autoRefreshInterval;
            }
        }
    }

    public void OnSearchToggled(bool isOn)
    {
        Debug.LogError("Lanzo manual");
        RefreshLobbyList();
    }

    public void OnDisconnectToggled(bool isOn)
    {
        Debug.LogError("*** ME DESCONECTO ***");
        ForceDisconnect();
        connectLabel.text = "Crear host";
        enableToggles();

    }



    

    private void SetupPaginationButtons()
    {
        if (previousButton != null)
        {
            previousButton.onValueChanged.AddListener((isOn) =>
            {
                if (isOn) PreviousPage();
            });
        }

        if (nextButton != null)
        {
            nextButton.onValueChanged.AddListener((isOn) =>
            {
                if (isOn) NextPage();
            });
        }

        UpdatePaginationUI();
    }

    private string GetUniqueProfile()
    {
        #if UNITY_EDITOR
        string editorProfile = $"Editor_{System.Guid.NewGuid().ToString("N").Substring(0, 20)}";
        Debug.Log($"🖥 Modo Editor: Perfil único generado");
        return editorProfile;
        
        #elif UNITY_ANDROID
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        string deviceProfile = $"Device_{deviceId.Substring(0, 20)}";
        Debug.Log($"📱 Modo Dispositivo: Usando Device ID");
        return deviceProfile;
        
        #else
        string fallbackProfile = $"Player_{System.Guid.NewGuid().ToString("N").Substring(0, 20)}";
        Debug.Log($"⚠ Plataforma desconocida: Usando GUID");
        return fallbackProfile;
        #endif
    }

    async System.Threading.Tasks.Task InitializeServices()
    {
        if (servicesInitialized) return;

        Debug.Log("====== INICIANDO SERVICIOS ======");
        Debug.Log($"🔧 Project ID: {Application.cloudProjectId}");
        
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                Debug.Log("Inicializando Unity Services...");
                
                string profile = GetUniqueProfile();
                
                Debug.Log($"✓ Perfil: {profile}");
                Debug.Log($"   Plataforma: {Application.platform}");
                Debug.Log($"   Es Editor: {Application.isEditor}");
                
                var options = new InitializationOptions();
                options.SetProfile(profile);
                
                await UnityServices.InitializeAsync(options);
                Debug.Log($"✓ Unity Services inicializado");
            }
            else
            {
                Debug.Log($"Unity Services ya inicializado (Estado: {UnityServices.State})");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Autenticando jugador...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"✓ Autenticado como: {AuthenticationService.Instance.PlayerId}");
            }
            else
            {
                Debug.Log($"✓ Ya autenticado como: {AuthenticationService.Instance.PlayerId}");
            }
            
            servicesInitialized = true;
            SetStatus("Listo");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error al inicializar servicios: {e.Message}\n{e.StackTrace}");
            SetStatus("Error de inicialización");
            throw;
        }
    }

    public async void RefreshLobbyList()
    {
        if (isRefreshing) return;
        
        if (!servicesInitialized)
        {
            await InitializeServices();
            return;
        }

        isRefreshing = true;
        SetStatus("Buscando lobbies...");

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        QueryFilter.FieldOptions.AvailableSlots,
                        "0",
                        QueryFilter.OpOptions.GT)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

            allLobbies = response.Results;
            currentPage = 0;
            
            totalPages = Mathf.CeilToInt((float)allLobbies.Count / lobbiesPerPage);
            
            if (allLobbies.Count == 0)
            {
                SetStatus("No hay lobbies disponibles");
                totalPages = 1;
            }
            else
            {
                SetStatus($"{allLobbies.Count} lobby(s) encontrado(s)");
                Debug.Log($"[LobbyMenu] Se encontraron {allLobbies.Count} lobbies");
            }

            DisplayCurrentPage();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyMenu] Error buscando lobbies: {e}");
            SetStatus("Error al buscar lobbies");
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void DisplayCurrentPage()
    {
        ClearAllSlots();

        int startIndex = currentPage * lobbiesPerPage;
        int endIndex = Mathf.Min(startIndex + lobbiesPerPage, allLobbies.Count);

        if (allLobbies.Count == 0)
        {
            CreateNoLobbiesMessage(lobbySlots[0]);
        }
        else
        {
            for (int i = startIndex; i < endIndex; i++)
            {
                int slotIndex = i - startIndex;
                CreateLobbyRow(allLobbies[i], lobbySlots[slotIndex]);
            }
        }

        UpdatePaginationUI();
    }

    private void ClearAllSlots()
    {
        foreach (Transform slot in lobbySlots)
        {
            if (slot == null)
            {
                Debug.LogError("[LobbyMenu] Uno de los slots no está asignado");
                continue;
            }

            foreach (Transform child in slot)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void CreateLobbyRow(Lobby lobby, Transform parentSlot)
    {
        if (lobbyRowPrefab == null)
        {
            Debug.LogError("[LobbyMenu] lobbyRowPrefab no está asignado");
            return;
        }

        if (parentSlot == null)
        {
            Debug.LogError("[LobbyMenu] parentSlot es null");
            return;
        }

        GameObject row = Instantiate(lobbyRowPrefab, parentSlot);

        TMP_Text lobbyNameText = FindChildByName(row.transform, "LobbyName")?.GetComponent<TMP_Text>();
        TMP_Text playersText = FindChildByName(row.transform, "Players")?.GetComponent<TMP_Text>();
        Toggle joinButton = FindChildByName(row.transform, "Join")?.GetComponent<Toggle>();

        if (lobbyNameText != null)
        {
            lobbyNameText.text = lobby.Name;
        }

        if (playersText != null)
        {
            playersText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        }

        if (joinButton != null)
        {
            joinButton.onValueChanged.RemoveAllListeners();
            joinButton.onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                {
                    JoinLobby(lobby.Id);
                }
            });
        }

        Debug.Log($"[LobbyMenu] Creada fila para lobby: {lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers})");
    }

    private void CreateNoLobbiesMessage(Transform parentSlot)
    {
        if (parentSlot == null) return;

        GameObject messageObj = new GameObject("NoLobbiesMessage");
        messageObj.transform.SetParent(parentSlot, false);
        
        RectTransform rect = messageObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        
        TMP_Text text = messageObj.AddComponent<TextMeshProUGUI>();
        text.text = "No hay lobbies disponibles";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24;
        text.color = new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    private void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            DisplayCurrentPage();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages - 1)
        {
            currentPage++;
            DisplayCurrentPage();
        }
    }

    private void UpdatePaginationUI()
    {
        if (previousButton != null)
        {
            previousButton.interactable = currentPage > 0;
        }

        if (nextButton != null)
        {
            nextButton.interactable = currentPage < totalPages - 1;
        }

        if (pageInfoText != null)
        {
            pageInfoText.text = $"{currentPage + 1}/{Mathf.Max(1, totalPages)}";
        }
    }

    async void JoinLobby(string lobbyId)
    {
        SetStatus("Uniéndose al lobby...");
        Debug.Log($"[LobbyMenu] Intentando unirse al lobby: {lobbyId}");

        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

            if (!joinedLobby.Data.ContainsKey(RELAY_CODE_KEY))
            {
                SetStatus("Esperando código de Relay...");
                Debug.Log("[LobbyMenu] Esperando código de Relay del host...");
                await WaitForRelayCode(joinedLobby.Id);
                return;
            }

            string relayJoinCode = joinedLobby.Data[RELAY_CODE_KEY].Value;
            await ConnectToRelay(relayJoinCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyMenu] Error uniéndose al lobby: {e}");
            SetStatus("No se pudo unir al lobby");
        }
    }


   /** private async Task StartAsClient()
    {   
        joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(foundLobby.Id);
        Debug.LogError("------ LANZO COMO CLIENT------");
        try
        {
            string joinCode = await WaitForRelayCode();

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Failed to get relay code from lobby");
                await LeaveLobby();
                return;
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData);

            NetworkManager.Singleton.StartClient();
            finishConnection();
            
            Debug.Log("Client started successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start as client: {e.Message}");
            await LeaveLobby();
            throw;
        }

    }**/

    async System.Threading.Tasks.Task WaitForRelayCode(string lobbyId)
    {
        string joinCode = null;
        int attempts = 0;
        const int maxAttempts = 20;

        while (joinCode == null && attempts < maxAttempts)
        {
            await System.Threading.Tasks.Task.Delay(500);
            
            try
            {
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);

                if (lobby.Data != null && lobby.Data.ContainsKey(RELAY_CODE_KEY))
                {
                    joinCode = lobby.Data[RELAY_CODE_KEY].Value;
                    Debug.Log("[LobbyMenu] Código de Relay recibido");
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbyMenu] Error obteniendo lobby: {e}");
                SetStatus("Lobby no disponible");
                return;
            }

            attempts++;
        }

        if (joinCode != null)
        {
            await ConnectToRelay(joinCode);
        }
        else
        {
            Debug.LogError("[LobbyMenu] Timeout esperando código de Relay");
            SetStatus("Timeout esperando código de Relay");
        }
    }

    async System.Threading.Tasks.Task ConnectToRelay(string joinCode)
    {
        SetStatus("Conectando a Relay...");
        Debug.Log($"[LobbyMenu] Conectando a Relay con código: {joinCode}");

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData);

            NetworkManager.Singleton.StartClient();
            SetStatus("¡Conectado!");
            finishConnection();
            //avatarDropdownManager.spawnAvatar();
            Debug.Log("[LobbyMenu] Conectado exitosamente al Relay");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyMenu] Error conectando a Relay: {e}");
            SetStatus("Error de conexión");
        }
    }

    Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform result = FindChildByName(child, name);
            if (result != null)
                return result;
        }
        return null;
    }


    private void finishConnection() {
        if (resetToggle != null)
            resetToggle.interactable = true;
        
        if (updateToggle != null)
            updateToggle.interactable = true;
        
        if (startToggle != null)
            startToggle.interactable = true;

        if (createHostToggle != null)
            createHostToggle.interactable = true;

        if (searchButton != null)
            searchButton.interactable = true;

        if (previousButton != null)
            previousButton.interactable = true;

        if (nextButton != null)
            nextButton.interactable = true;
        
        if (disconnectToggle != null)
            disconnectToggle.interactable = true;

        if (addInvaderToggle != null)
            addInvaderToggle.interactable = true;

        if (removeInvaderToggle != null)
            removeInvaderToggle.interactable = true;
        
    }

    private void enableToggles() {
        if (resetToggle != null)
            resetToggle.interactable = false;
        
        if (updateToggle != null)
            updateToggle.interactable = false;
        
        if (startToggle != null)
            startToggle.interactable = false;
        
        if (addInvaderToggle != null)
            addInvaderToggle.interactable = false;

        if (removeInvaderToggle != null)
            removeInvaderToggle.interactable = false;

        if (createHostToggle != null)
            createHostToggle.interactable = true;

        if (searchButton != null)
            searchButton.interactable = true;

        if (previousButton != null)
            previousButton.interactable = false;

        if (nextButton != null)
            nextButton.interactable = false;

        if (disconnectToggle != null)
            disconnectToggle.interactable = false;
        
    }      

    
    
    void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;

        Debug.Log($"[LobbyMenu] Status: {msg}");
    }


    public bool IsConnected()
    {
        return joinedLobby != null || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
    }

    public async void ForceDisconnect()
    {
        Debug.Log("[LobbyMenuManager] Desconexión forzada desde AutoLobbyBezi");
        
        // Shutdown de NetworkManager
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            await System.Threading.Tasks.Task.Delay(200);
        }
        
        // Salir del lobby si existe
        if (joinedLobby != null)
        {
            try
            {
                string lobbyId = joinedLobby.Id;
                string playerId = AuthenticationService.Instance.PlayerId;

                await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                Debug.Log("[LobbyMenuManager] Salido del lobby");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[LobbyMenuManager] Error al salir del lobby: {e.Message}");
            }
            finally
            {
                joinedLobby = null;
            }
        }
        
        SetStatus("Desconectado");
    }


}
