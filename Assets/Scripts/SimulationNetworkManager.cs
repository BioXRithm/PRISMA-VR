using UnityEngine;
using Unity.Netcode;

public class SimulationNetworkManager : NetworkBehaviour
{
    public static SimulationNetworkManager Instance { get; private set; }

    public GameObject predatorPrefab;
    public GameObject preyPrefab;

    public GameObject invaderPrefab;

    private Transform spawnPredators;
    private Transform spawnPreys;
    private Transform spawnInvaders;

    private Terrain terrain;
    private VolterraStatus volterra;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        GameObject t = GameObject.Find("MainTerrainDef");
        terrain = t.GetComponent<Terrain>();

        spawnPredators = GameObject.Find("PredatorParents").transform;
        spawnPreys = GameObject.Find("PreyParent").transform;
        spawnInvaders = GameObject.Find("InvaderParent").transform;

        GameObject go = GameObject.Find("VolterraStatus");
        volterra = go.GetComponent<VolterraStatus>();

        Debug.Log($"✓ SimulationNetworkManager spawned. IsServer={IsServer}");
    }

    public void RequestChangeStochastic(bool isActive, float sigma)
    {
        //Debug.Log($"🎮 RequestStartSimulation: predators={predators}, preys={preys}, firstTime={firstTime}");
        changeStochasticServerRpc(isActive, sigma);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void changeStochasticServerRpc(bool isActive, float sigma)
    {
        //Debug.Log($"🎮 [SERVER] AddInvadrRpc ejecutado, firstTime={invaders}");

            //SpawnModels(invaders, spawnInvaders, invaderPrefab);
            volterra.setStochasticServerRpc(isActive, sigma);
        
        //NotifySimulationStartedClientRpc();
    }

    public void RequestChangeFire(bool isActive, int intensity)
    {
        //Debug.Log($"🎮 RequestStartSimulation: predators={predators}, preys={preys}, firstTime={firstTime}");
        changeFireServerRpc(isActive, intensity);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void changeFireServerRpc(bool isActive, int intensity)
    {
            volterra.setFireServerRpc(isActive, intensity);
    }

    public void RequestChangeLoad(bool isActive, int load)
    {
        //Debug.Log($"🎮 RequestStartSimulation: predators={predators}, preys={preys}, firstTime={firstTime}");
        changeLoadServerRpc(isActive, load);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void changeLoadServerRpc(bool isActive, int load)
    {
        //Debug.Log($"🎮 [SERVER] AddInvadrRpc ejecutado, firstTime={invaders}");

            //SpawnModels(invaders, spawnInvaders, invaderPrefab);
            volterra.setLoadServerRpc(isActive, load);
        
        //NotifySimulationStartedClientRpc();
    }

    public void RequestStartSimulation(int predators, int preys, float alpha, float beta, float gamma, float delta, bool firstTime, float time, int equation)
    {
        Debug.Log($"🎮 RequestStartSimulation: predators={predators}, preys={preys}, firstTime={firstTime}");
        StartSimulationServerRpc(predators, preys, alpha, beta, gamma, delta, firstTime, time, equation);
    }

    public void RequestAddInvader(int invader, float eta, float zeta, float epsilon, float omega)
    {
        Debug.Log($"🎮 RequestAddInvader: invaders={invader}");
        AddInvaderServerRpc(invader, eta, zeta, epsilon, omega);
    }

    public void RequestRemoveInvader()
    {
        Debug.Log($"🎮 RequestRemoveInvader: invaders=");
        RemoveInvaderServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RemoveInvaderServerRpc()
    {
        Debug.Log("🗑 [SERVER] DestroyCurrentModelsServerRpc ejecutado");
        DestroyInvaders();
    }

    public void RequestStopSimulation()
    {
        Debug.Log("⏸ RequestStopSimulation");
        StopSimulationServerRpc();
    }

    public void RequestDestroyModels()
    {
        Debug.Log("🗑 RequestDestroyModels");
        DestroyCurrentModelsServerRpc();
    }

    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void AddInvaderServerRpc(int invaders, float eta, float zeta, float epsilon, float omega)
    {
        Debug.Log($"🎮 [SERVER] AddInvadrRpc ejecutado, firstTime={invaders}");

            //SpawnModels(invaders, spawnInvaders, invaderPrefab);
            volterra.setInvadersServerRpc(invaders, eta, zeta, epsilon, omega);
        
        //NotifySimulationStartedClientRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void StartSimulationServerRpc(int predators, int preys, float alpha, float beta, float gamma, float delta, bool firstTime, float time, int equation)
    {
        Debug.Log($"🎮 [SERVER] StartSimulationServerRpc ejecutado, firstTime={firstTime}");
        
        if (firstTime)
        {
            SpawnModels(predators, spawnPredators, predatorPrefab);
            SpawnModels(preys, spawnPreys, preyPrefab);
            if (volterra.withInvader.Value)
            {
                SpawnModels(Mathf.CeilToInt(volterra.invaders.Value), spawnInvaders, invaderPrefab);
            }
            volterra.setValuesServerRpc(alpha, beta, gamma, delta, predators, preys, time, equation);
        }
        
        volterra.startSimulation();
        
        NotifySimulationStartedClientRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void StopSimulationServerRpc()
    {
        Debug.Log("⏸ [SERVER] StopSimulationServerRpc ejecutado");
        volterra.stopSimulation();
        NotifySimulationStoppedClientRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void DestroyCurrentModelsServerRpc()
    {
        Debug.Log("🗑 [SERVER] DestroyCurrentModelsServerRpc ejecutado");
        DestroyCurrentModels();
        NotifyModelsDestroyedClientRpc();
    }

    [ClientRpc]
    void NotifySimulationStartedClientRpc()
    {
        Debug.Log("📡 [CLIENT] Simulación iniciada");
        var startSim = FindFirstObjectByType<StartSimulation>();
        if (startSim != null)
        {
            startSim.OnSimulationStarted();
        }
    }

    [ClientRpc]
    void NotifySimulationStoppedClientRpc()
    {
        Debug.Log("📡 [CLIENT] Simulación detenida");
        var startSim = FindFirstObjectByType<StartSimulation>();
        if (startSim != null)
        {
            startSim.OnSimulationStopped();
        }
    }

    [ClientRpc]
    void NotifyModelsDestroyedClientRpc()
    {
        Debug.Log("📡 [CLIENT] Modelos destruidos");
    }

    void DestroyCurrentModels()
    {
        if (!IsServer)
        {
            Debug.LogError("❌ DestroyCurrentModels solo debe ejecutarse en el servidor");
            return;
        }

        int destroyedCount = 0;

        if (spawnPredators != null)
        {
            foreach (Transform child in spawnPredators)
            {
                NetworkObject netObj = child.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                Destroy(child.gameObject);
                destroyedCount++;
            }
        }

        if (spawnPreys != null)
        {
            foreach (Transform child in spawnPreys)
            {
                NetworkObject netObj = child.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                Destroy(child.gameObject);
                destroyedCount++;
            }
        }
        if (volterra.withInvader.Value) {
            DestroyInvaders();
        }

        Debug.Log($"✓ [SERVER] Destruidos {destroyedCount} modelos");
    }


    void DestroyInvaders()
    {
        if (!IsServer)
        {
            Debug.LogError("❌ DestroyCurrentModels solo debe ejecutarse en el servidor");
            return;
        }

        int destroyedCount = 0;
        if (spawnInvaders != null)
        {
            foreach (Transform child in spawnInvaders)
            {
                NetworkObject netObj = child.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                Destroy(child.gameObject);
                destroyedCount++;
            }
        }

        Debug.Log($"✓ [SERVER] Destruidos {destroyedCount} modelos");
    }

    void SpawnModels(int number, Transform spawnParent, GameObject modelPrefab)
    {
        if (!IsServer)
        {
            Debug.LogError("❌ SpawnModels llamado desde cliente");
            return;
        }

        Vector3 terrainPosition = terrain.GetPosition();
        Vector3 terrainSize = terrain.terrainData.size;

        for (int i = 0; i < number; i++)
        {
            float x = Random.Range(terrainPosition.x, terrainPosition.x + terrainSize.x);
            float z = Random.Range(terrainPosition.z, terrainPosition.z + terrainSize.z);
            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrainPosition.y;

            Vector3 position = new Vector3(x, y, z);
            GameObject model = Instantiate(modelPrefab, position, Quaternion.identity, spawnParent);
            NetworkObject netObj = model.GetComponent<NetworkObject>();
            netObj.Spawn();
            model.transform.SetParent(spawnParent, true);
            
            float bottomOffset = GetBottomOffset(model);
            model.transform.position = new Vector3(position.x, position.y + bottomOffset, position.z);
        }
        
        Debug.Log($"✓ [SERVER] Spawned {number} modelos");
    }

    float GetBottomOffset(GameObject obj)
    {
        Renderer renderer = obj.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            float pivotY = obj.transform.position.y;
            float bottomY = renderer.bounds.min.y;
            return pivotY - bottomY;
        }
        return 0f;
    }
}
