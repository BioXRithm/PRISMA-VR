using TMPro;
using UnityEngine.UI;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Oculus.Interaction.Samples;

public class AutoLobbyBezi : MonoBehaviour
{
    private const string RELAY_CODE_KEY = "relayCode";
    private const int MIN_PLAYERS = 2;
    private const int MAX_PLAYERS_LIMIT = 100;
    private const float HEARTBEAT_INTERVAL = 15f;
    private const float CLIENT_POLL_TIMEOUT = 30f;
    private const float CLIENT_POLL_INTERVAL = 0.5f;

    public Toggle submitButton;
    public TMP_InputField maxPlayers;
    [Header("Connection Status UI")]
    public TMP_Text connectLabel;

    private int maxPlayersCount = 5;
    private Lobby joinedLobby;
    private bool isConnecting;
    private CancellationTokenSource heartbeatCancellation;


    [Header("UI Elements - Toggles")]
    public Toggle resetToggle;
    public Toggle updateToggle;
    public Toggle startToggle;
    public Toggle createHostToggle;
    public Toggle searchLobbyToggle;
    public Toggle prevToggle;
    public Toggle nextToggle;
    //public Toggle connectToggle;
    public Toggle addInvaderToggle;
    public Toggle removeInvaderToggle;

    [Header("UI Elements - GameObjects")]
    public GameObject configuration;
    public GameObject online;

    [Header("UI References - Status")]
    [Tooltip("Texto para mostrar el estado actual")]
    public TMP_Text statusText;



    void Start()
    {
        if (submitButton != null)
        {
            submitButton.onValueChanged.AddListener(OnSubmitButtonToggled);
        }
        else
        {
            Debug.LogError("Submit button not assigned!");
        }
    }

    void OnDestroy()
    {
        if (submitButton != null)
        {
            submitButton.onValueChanged.RemoveListener(OnSubmitButtonToggled);
        }

        CleanupLobby();
    }

    async void OnApplicationQuit()
    {
        CleanupLobby();
        await Task.Delay(100);
    }
    public async void OnSubmitButtonToggled(bool isOn)
    {
        Debug.LogError("------ SE PULSA CONECTAR ------");
        connectLabel.text = "Conectando";
        
        if (isConnecting)
        {
            Debug.LogWarning("Connection already in progress");
            return;
        }

        if (!ValidateMaxPlayers())
        {
            return;
        }

        isConnecting = true;

        try
        {
            // Buscar LobbyMenuManager y desconectar si está conectado
            GameObject lobbyManagerObj = GameObject.Find("LobbyManager");
            if (lobbyManagerObj != null)
            {
                LobbyMenuManager lobbyMenuManager = lobbyManagerObj.GetComponent<LobbyMenuManager>();
                
                if (lobbyMenuManager != null && lobbyMenuManager.IsConnected())
                {
                    Debug.Log("⚠️ Detectada conexión desde LobbyMenuManager. Desconectando...");
                    lobbyMenuManager.ForceDisconnect();
                    
                    // Esperar a que termine la desconexión
                    await Task.Delay(800);
                }
            }
            
            // Verificar si NetworkManager sigue activo
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("⚠️ NetworkManager aún activo. Forzando shutdown...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(300);
            }
            
            Debug.LogError("------ CREANDO NUEVO LOBBY COMO HOST ------");
            await JoinOrCreateLobby();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to connect: {e.Message}");
            isConnecting = false;
        }
    }


    private bool ValidateMaxPlayers()
    {
        if (maxPlayers == null || string.IsNullOrEmpty(maxPlayers.text))
        {
            Debug.LogError("Max players input field is empty");
            return false;
        }

        if (!int.TryParse(maxPlayers.text, out int players))
        {
            Debug.LogError("Invalid max players value");
            return false;
        }

        if (players < MIN_PLAYERS || players > MAX_PLAYERS_LIMIT)
        {
            Debug.LogError($"Max players must be between {MIN_PLAYERS} and {MAX_PLAYERS_LIMIT}");
            return false;
        }

        maxPlayersCount = players;
        Debug.Log($"Max players set to: {maxPlayersCount}");
        return true;
    }



private string GetUniqueProfile()
{
    #if UNITY_EDITOR
    // EN EDITOR: Siempre crear nuevo perfil para permitir múltiples instancias
    string editorProfile = $"Editor_{System.Guid.NewGuid().ToString("N").Substring(0, 20)}";
    Debug.Log($"🖥 Modo Editor: Perfil único generado");
    return editorProfile;
    
    #elif UNITY_ANDROID
    // EN DISPOSITIVO ANDROID (Meta Quest): Usar Device ID único
    string deviceId = SystemInfo.deviceUniqueIdentifier;
    string deviceProfile = $"Device_{deviceId.Substring(0, 20)}";
    Debug.Log($"📱 Modo Dispositivo: Usando Device ID");
    return deviceProfile;
    
    #else
    // OTROS CASOS: Fallback a GUID
    string fallbackProfile = $"Player_{System.Guid.NewGuid().ToString("N").Substring(0, 20)}";
    Debug.Log($"⚠ Plataforma desconocida: Usando GUID");
    return fallbackProfile;
    #endif
}



private async Task InitializeServices()
{
    Debug.Log("====== Creando hosst ======");
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
    }
    catch (System.Exception e)
    {
        Debug.LogError($"❌ Error al inicializar servicios: {e.Message}\n{e.StackTrace}");
        throw;
    }
}

private async Task JoinOrCreateLobby()
{
    try
    {
        Debug.Log("====== BUSCANDO/CREANDO LOBBY ======");
        Debug.Log($"🔧 Project ID: {Application.cloudProjectId}");
        Debug.Log($"🔧 Player ID: {AuthenticationService.Instance.PlayerId}");
        
        //Lobby foundLobby = null;
        //int retries = 6;
        //float delaySeconds = 2f;

       /** for (int attempt = 0; attempt < retries; attempt++)
        {
            if (attempt > 0)
            {
                Debug.Log($"🔄 Reintento {attempt}/{retries - 1} esperando {delaySeconds}s...");
                await Task.Delay((int)(delaySeconds * 1000));
            }

            try
            {
                var queryOptions = new QueryLobbiesOptions
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

                Debug.Log($"🔍 [Intento {attempt + 1}/{retries}] Buscando lobbies...");
                var lobbies = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

                Debug.Log($"📊 Lobbies encontrados: {lobbies.Results.Count}");

                if (lobbies.Results.Count > 0)
                {
                    Debug.Log("--- LOBBIES DISPONIBLES ---");
                    for (int i = 0; i < lobbies.Results.Count; i++)
                    {
                        var lobby = lobbies.Results[i];
                        Debug.Log($"  [{i}] '{lobby.Name}'");
                        Debug.Log($"      ID: {lobby.Id}");
                        Debug.Log($"      Jugadores: {lobby.Players.Count}/{lobby.MaxPlayers}");
                        Debug.Log($"      Slots: {lobby.AvailableSlots}");
                        Debug.Log($"      Privado: {lobby.IsPrivate}");
                        Debug.Log($"      Bloqueado: {lobby.IsLocked}");
                    }
                    Debug.Log("---------------------------");

                    foundLobby = lobbies.Results[0];
                    break;
                }
                else
                {
                    Debug.Log($"⚠ Ningún lobby encontrado en intento {attempt + 1}");
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"❌ Error al buscar lobbies: {e.Message}");
                Debug.LogError($"   Reason: {e.Reason}");
            }
        }

        if (foundLobby != null)
        {
            Debug.Log($"🚪 UNIÉNDOSE a lobby: '{foundLobby.Name}'");
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(foundLobby.Id);
            Debug.Log($"✓✓✓ UNIDO EXITOSAMENTE ✓✓✓");
        }
        else
        {**/
            Debug.Log("🏗 CREANDO NUEVO LOBBY...");
            string lobbyName = $"ClassRoom_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            
            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayersCount,
                new CreateLobbyOptions
                {
                    IsPrivate = false,
                    IsLocked = false,
                    Data = new Dictionary<string, DataObject>()
                });

            Debug.Log($"✓✓✓ LOBBY CREADO ✓✓✓");
            Debug.Log($"    Nombre: {joinedLobby.Name}");
            Debug.Log($"    ID: {joinedLobby.Id}");
        //}

        Debug.Log($"====== ESTADO FINAL ======");
        Debug.Log($"Nombre: {joinedLobby.Name}");
        Debug.Log($"Players: {joinedLobby.Players.Count}/{joinedLobby.MaxPlayers}");
        
        await HandleLobbyRole();
    }
    catch (LobbyServiceException e)
    {
        Debug.LogError($"❌ Lobby Service Error: {e.Message}");
        Debug.LogError($"   Reason: {e.Reason}");
        isConnecting = false;
        throw;
    }
}



    private async Task HandleLobbyRole()
    {
        Debug.Log("Determining lobby role...");
        
        if (joinedLobby.Players[0].Id == AuthenticationService.Instance.PlayerId)
        {
            Debug.Log("Starting as host");
            await StartAsHost();
        }
        else
        {
            Debug.Log("Starting as client");
            await StartAsClient();
        }

        isConnecting = false;
    }

    private async Task StartAsHost()
    {
        Debug.LogError("------ LANZO COMO HOST ------");
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayersCount - 1);

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            RELAY_CODE_KEY,
                            new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                        }
                    }
                });

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData);

            NetworkManager.Singleton.StartHost();
            finishConnection();
            SetStatus("Lobby creado con éxito");
            StartLobbyHeartbeat();

            
            Debug.Log("Host started successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start as host: {e.Message}");
            await LeaveLobby();
            throw;
        }
    }

    private async Task StartAsClient()
    {
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

    }

    private async Task<string> WaitForRelayCode()
    {
        float elapsedTime = 0f;

        while (elapsedTime < CLIENT_POLL_TIMEOUT)
        {
            try
            {
                joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

                if (joinedLobby.Data != null && joinedLobby.Data.ContainsKey(RELAY_CODE_KEY))
                {
                    string joinCode = joinedLobby.Data[RELAY_CODE_KEY].Value;
                    if (!string.IsNullOrEmpty(joinCode))
                    {
                        return joinCode;
                    }
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"Error fetching lobby: {e.Message}");
            }

            await Task.Delay((int)(CLIENT_POLL_INTERVAL * 1000));
            elapsedTime += CLIENT_POLL_INTERVAL;
        }

        Debug.LogError($"Timeout waiting for relay code after {CLIENT_POLL_TIMEOUT} seconds");
        return null;
    }

    private void StartLobbyHeartbeat()
    {
        heartbeatCancellation?.Cancel();
        heartbeatCancellation = new CancellationTokenSource();
        
        _ = LobbyHeartbeatAsync(heartbeatCancellation.Token);
    }

    private async Task LobbyHeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && joinedLobby != null)
        {
            try
            {
                await Task.Delay((int)(HEARTBEAT_INTERVAL * 1000), cancellationToken);
                
                if (!cancellationToken.IsCancellationRequested)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
                    Debug.Log("Lobby heartbeat sent");
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"Heartbeat failed: {e.Message}");
                break;
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
        }
    }

    private void CleanupLobby()
    {
        heartbeatCancellation?.Cancel();
        
        if (joinedLobby != null)
        {
            _ = LeaveLobby();
        }
    }

    private async Task LeaveLobby()
    {
        if (joinedLobby == null) return;

        try
        {
            bool isHost = joinedLobby.Players[0].Id == AuthenticationService.Instance.PlayerId;
            string lobbyId = joinedLobby.Id;

            await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
            
            if (isHost)
            {
                try
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                    Debug.Log("Lobby deleted");
                }
                catch (LobbyServiceException)
                {
                    Debug.Log("Lobby already deleted or expired");
                }
            }
            else
            {
                Debug.Log("Left lobby");
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Error leaving lobby: {e.Message}");
        }
        finally
        {
            joinedLobby = null;
        }
    }

    void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;

        Debug.Log($"[LobbyMenu] Status: {msg}");
    }

    private void finishConnection() {
            connectLabel.text = "Conectado";
            if (resetToggle != null)
                resetToggle.interactable = true;
            
            if (updateToggle != null)
                updateToggle.interactable = true;
            
            if (startToggle != null)
                startToggle.interactable = true;

            if (createHostToggle != null)
                createHostToggle.interactable = false;

            if (searchLobbyToggle != null)
                searchLobbyToggle.interactable = false;

            if (prevToggle != null)
                prevToggle.interactable = false;

             if (nextToggle != null)
                nextToggle.interactable = false;
            
            if (submitButton != null)
               submitButton.interactable = false;
            
            if (addInvaderToggle != null)
                addInvaderToggle.interactable = true;

            if (removeInvaderToggle != null)
                removeInvaderToggle.interactable = true;
            
            /**if (configuration != null)
                configuration.SetActive(true);
            
            if (online != null)
                online.SetActive(false);**/
    }
}
