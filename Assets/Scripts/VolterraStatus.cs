using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using XCharts.Runtime;
using Unity.Netcode;

public class VolterraStatus : NetworkBehaviour
{
    private const int MAX_INSTANCES = 100;

    [Header("Prefabs")]
    public GameObject predatorPrefab;
    public GameObject preyPrefab;
    public GameObject invaderPrefab;

    [Header("Spawn Points")]
    public Transform spawnPredators;
    public Transform spawnPreys;
    public Transform spawnInvaders;

    [Header("Charts")]
    public LineChart lineChart;
    public LineChart timeChart;

    [Header("Lotka-Volterra Parameters (2 species)")]
    public NetworkVariable<float> alpha = new NetworkVariable<float>(0f);
    public NetworkVariable<float> beta = new NetworkVariable<float>(0f);
    public NetworkVariable<float> gamma = new NetworkVariable<float>(0f);
    public NetworkVariable<float> delta = new NetworkVariable<float>(0f);

    [Header("Invader Parameters (3 species)")]
    public NetworkVariable<float> eta = new NetworkVariable<float>(0f);
    public NetworkVariable<float> zeta = new NetworkVariable<float>(0f);
    public NetworkVariable<float> epsilon = new NetworkVariable<float>(0f);
    public NetworkVariable<float> omega = new NetworkVariable<float>(0f);

    public NetworkVariable<bool> withInvader = new NetworkVariable<bool>(false);

    [Header("Populations")]
    public NetworkVariable<float> preys = new NetworkVariable<float>(10);
    public NetworkVariable<float> invaders = new NetworkVariable<float>(3);
    public NetworkVariable<float> predators = new NetworkVariable<float>(3);

    [Header("References")]
    public LogSender logSender;
    public Terrain terrain;

    public GameObject fireContainer;

    public NetworkVariable<float> deltaT = new NetworkVariable<float>(0.1f);

    private NetworkVariable<int> preysScene = new NetworkVariable<int>(0);
    private NetworkVariable<int> invadersScene = new NetworkVariable<int>(0);
    private NetworkVariable<int> predatorsScene = new NetworkVariable<int>(0);

    private NetworkVariable<int> equation = new NetworkVariable<int>(0);

    // Carrying capacity (capacidad de carga)
    private NetworkVariable<int> load = new NetworkVariable<int>(0);
    private NetworkVariable<bool> loadActive = new NetworkVariable<bool>(false);

    // Stochasticity (ruido ambiental)
    private NetworkVariable<bool> stochastic = new NetworkVariable<bool>(false);
    private NetworkVariable<float> sigma = new NetworkVariable<float>(0.02f);

    private NetworkVariable<bool> fireActive = new NetworkVariable<bool>(false);
    private NetworkVariable<int> fireIntensity = new NetworkVariable<int>(0);

    private Coroutine simulationCoroutine;

    // --- Graph helpers ---

    public void resetGraph()
    {
        for (int i = 0; i < lineChart.series.Count; i++)
            lineChart.GetSerie(i).ClearData();

        for (int i = 0; i < timeChart.series.Count; i++)
            timeChart.GetSerie(i).ClearData();
    }

    // --- Population modification (called by animal scripts) ---

    public void removePredator(int n, GameObject wolf)
    {
        if (!IsServer) return;

        this.predators.Value += n;
        destroyWolf(wolf);
    }

    public void removePrey(int n, GameObject rabbit)
    {
        if (!IsServer) return;

        this.preys.Value += n;
        Destroy(rabbit);
    }

    // --- Chart data ---

    private void addPhaseAndTimeData()
    {
        int predInt = Mathf.CeilToInt(this.predators.Value);
        int preyInt = Mathf.CeilToInt(this.preys.Value);

        // Time chart
        int nextX = timeChart.GetSerie(0).dataCount + 1;
        timeChart.AddData(0, new double[] { nextX, predInt });
        timeChart.AddData(1, new double[] { nextX, preyInt });

        // Phase chart
        lineChart.AddData(0, new double[] { predInt, preyInt });

        if (this.withInvader.Value)
        {
            int invInt = Mathf.CeilToInt(this.invaders.Value);
            timeChart.AddData(2, new double[] { nextX, invInt });
            lineChart.AddData(1, new double[] { invInt, preyInt });
        }
    }

    private void addPoint()
    {
        addPhaseAndTimeData();

        if (IsServer)
        {
            UpdateGraphClientRpc();
        }
    }

    [Rpc(SendTo.NotServer)]
    private void UpdateGraphClientRpc()
    {
        addPhaseAndTimeData();
    }

    // --- Server RPCs ---

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void updateValuesServerRpc(float alpha, float beta, float gamma, float delta, float time, int preys, int predators)
    {
        this.alpha.Value = alpha;
        this.beta.Value = beta;
        this.gamma.Value = gamma;
        this.delta.Value = delta;
        this.deltaT.Value = time;
        this.predators.Value += predators;
        this.preys.Value += preys;
        Debug.Log("Valores de VolterraStatus actualizados en UPDATE VALUES.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void setValuesServerRpc(float alpha, float beta, float gamma, float delta, int predators, int preys, float time, int equation)
    {
        Debug.Log($"VolterraStatus spawned: {this.NetworkObject.IsSpawned} / IsServer: {IsServer}");
        this.alpha.Value = alpha;
        this.beta.Value = beta;
        this.gamma.Value = gamma;
        this.delta.Value = delta;
        this.predators.Value = predators;
        this.preys.Value = preys;
        this.deltaT.Value = time;
        this.preysScene.Value = preys;
        this.predatorsScene.Value = predators;
        this.equation.Value = equation;
        addPoint();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void setLoadServerRpc(bool isActive, int load)
    {
        this.load.Value = load;
        this.loadActive.Value = isActive;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void setStochasticServerRpc(bool isActive, float sigma)
    {
        this.stochastic.Value = isActive;
        this.sigma.Value = Mathf.Clamp01(sigma);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void setFireServerRpc(bool isActive, int intensity)
    {
        this.fireActive.Value = isActive;
        this.fireIntensity.Value = intensity;
        fireContainer.SetActive(isActive);

        // Al activar: desactivar hijos Heat Haze + subir SortingOrder del Canvas.
        // Al desactivar: FireHeatHazeFixer.OnDisable() restaura el Canvas.
        if (isActive)
        {
            DisableHeatDistortionInFires();
            BoostWorldSpaceCanvasSorting();
        }
    }

    /// <summary>
    /// Desactiva los hijos del fuego que usan shaders problemáticos con la UI.
    /// "Heat Distortion" y "Burning Dark" usan el shader Heat Haze que muestrea
    /// la textura opaca de cámara (SAMPLE_SCENE_COLOR), lo que "borra"
    /// visualmente los Canvas World-Space.
    /// </summary>
    private void DisableHeatDistortionInFires()
    {
        if (fireContainer == null) return;

        // Nombres de hijos que usan el shader Heat Haze (SAMPLE_SCENE_COLOR)
        string[] problematicChildren = { "Heat Distortion", "Burning Dark" };

        foreach (Transform fire in fireContainer.transform)
        {
            foreach (string childName in problematicChildren)
            {
                Transform child = fire.Find(childName);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Sube el SortingOrder de todos los Canvas World-Space para que se
    /// rendericen por encima de las partículas transparentes del fuego.
    /// Las partículas y el Canvas compiten en la misma cola "Transparent"
    /// con SortingOrder 0, haciendo que el Canvas parezca transparente.
    /// </summary>
    private void BoostWorldSpaceCanvasSorting()
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in allCanvases)
        {
            if (c != null && c.renderMode == RenderMode.WorldSpace)
            {
                c.overrideSorting = true;
                c.sortingOrder = 100;
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void setInvadersServerRpc(int invader, float eta, float zeta, float epsilon, float omega)
    {
        this.eta.Value = eta;
        this.zeta.Value = zeta;
        this.epsilon.Value = epsilon;
        this.omega.Value = omega;
        this.invaders.Value = invader;
        this.invadersScene.Value = invader;
        this.withInvader.Value = true;
    }

    // --- Simulation lifecycle ---

    public void startSimulation()
    {
        Debug.Log("Simulación iniciada.");
        simulationCoroutine = StartCoroutine(simulationLoop());
    }

    public void stopSimulation()
    {
        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
            Debug.Log("Simulación en standby.");
        }
    }

    private IEnumerator simulationLoop()
    {
        while (true)
        {
            float nextIterationTime = Time.unscaledTime + 1.0f;

            runVolterraIteration();
            Debug.Log($"Valores: alpha={this.alpha.Value}, predators={this.predators.Value}");

            while (Time.unscaledTime < nextIterationTime)
            {
                yield return null;
            }
        }
    }

    // --- Visual helpers ---

    private float GetBottomOffset(GameObject obj)
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

    private void destroyWolf(GameObject wolf)
    {
        if (wolf == null) return;

        WalkerWolf wolfScript = wolf.GetComponent<WalkerWolf>();
        if (wolfScript != null)
        {
            GameObject followingRabbit = wolfScript.getFollowingRabbit();
            if (followingRabbit != null)
            {
                RandomWalkerRabbit rabbitScript = followingRabbit.GetComponent<RandomWalkerRabbit>();
                if (rabbitScript != null)
                {
                    rabbitScript.RemoveHunter(wolf);
                }
            }
        }

        NetworkObject networkObject = wolf.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
        }
        else
        {
            Destroy(wolf);
        }
    }

    /// <summary>
    /// Generic despawn: removes (oldNumber - newNumber) children from the given parent Transform.
    /// </summary>
    private void despawnEntities(int oldNumber, int newNumber, Transform parent)
    {
        int toDestroy = oldNumber - newNumber;
        if (toDestroy <= 0) return;

        List<GameObject> all = new List<GameObject>();
        foreach (Transform child in parent)
        {
            all.Add(child.gameObject);
        }

        List<GameObject> toProcess = all.Count > toDestroy
            ? all.GetRange(all.Count - toDestroy, toDestroy)
            : all;

        foreach (GameObject entity in toProcess)
        {
            destroyWolf(entity);
        }
    }

    public void despawnWolves(int oldNumber, int newNumber)
    {
        despawnEntities(oldNumber, newNumber, spawnPredators);
    }

    public void despawnInvaders(int oldNumber, int newNumber)
    {
        despawnEntities(oldNumber, newNumber, spawnInvaders);
    }

    public void despawnPreys(int oldNumber, int newNumber)
    {
        var rabbits = this.spawnPreys.GetComponentsInChildren<RandomWalkerRabbit>();
        var rabbitsToDespawn = rabbits
            .OrderByDescending(r => r.huntingWolves.Count)
            .Take(oldNumber - newNumber)
            .ToList();

        foreach (var rabbit in rabbitsToDespawn)
        {
            Destroy(rabbit.gameObject);
        }
    }

    private void spawn(int oldNumber, int newNumber, Transform parent, GameObject prefab)
    {
        Vector3 terrainPosition = terrain.GetPosition();
        Vector3 terrainSize = terrain.terrainData.size;

        for (int i = oldNumber; i < newNumber; i++)
        {
            float x = Random.Range(terrainPosition.x, terrainPosition.x + terrainSize.x);
            float z = Random.Range(terrainPosition.z, terrainPosition.z + terrainSize.z);
            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrainPosition.y;

            Vector3 position = new Vector3(x, y, z);
            GameObject model = Instantiate(prefab, position, Quaternion.identity, parent);

            float bottomOffset = GetBottomOffset(model);
            model.transform.position = new Vector3(position.x, position.y + bottomOffset, position.z);
        }
    }

    private void updateVisualModels(int oldPredators, int newPredators, int oldPreys, int newPreys)
    {
        if (newPredators > oldPredators)
            spawn(oldPredators, newPredators, this.spawnPredators, this.predatorPrefab);
        else
            despawnWolves(oldPredators, newPredators);

        if (newPreys > oldPreys)
            spawn(oldPreys, newPreys, this.spawnPreys, this.preyPrefab);
        else
            despawnPreys(oldPreys, newPreys);
    }

    private void updateInvaderVisual(int oldInvaders, int newInvaders)
    {
        if (newInvaders > oldInvaders)
            spawn(oldInvaders, newInvaders, this.spawnInvaders, this.invaderPrefab);
        else
            despawnInvaders(oldInvaders, newInvaders);
    }

    private int calculateNormalizedVisual(float currentNumber)
    {
        float totalPopulation = this.withInvader.Value
            ? this.predators.Value + this.preys.Value + this.invaders.Value
            : this.predators.Value + this.preys.Value;

        if (totalPopulation <= 0f)
            return 0;

        float result = (currentNumber * MAX_INSTANCES) / totalPopulation;
        return Mathf.CeilToInt(result);
    }

    // --- Carrying capacity safe accessor ---

    /// <summary>
    /// Returns the carrying capacity factor (1 - population/K).
    /// Returns 1 if carrying capacity is not active or K == 0 (no limit).
    /// </summary>
    private float getCarryingCapacityFactor(float population)
    {
        if (!this.loadActive.Value || this.load.Value <= 0)
            return 1f;
        Debug.Log("------- SE APLICA CARGA DEL ENTORNO---");
        return 1f - (population / (float)this.load.Value);
    }

    // --- Stochastic noise ---

    /// <summary>
    /// Applies multiplicative noise to all populations after the deterministic step.
    /// Uses σ * √h * N(0,1) approximation (Euler-Maruyama style).
    /// </summary>
    private void applyStochasticNoise(float h)
    {
        if (!this.stochastic.Value || this.sigma.Value <= 0f) return;
        Debug.Log("------- SE APLICA RUIDO ESTOCASTICO----");
        float s = this.sigma.Value;
        float sqrtH = Mathf.Sqrt(h);

        // Box-Muller transform for normally distributed noise
        float gaussianNoise()
        {
            float u1 = Random.Range(0.0001f, 1f);
            float u2 = Random.Range(0f, 1f);
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        }

        this.preys.Value = Mathf.Max(0, this.preys.Value + this.preys.Value * s * sqrtH * gaussianNoise());
        this.predators.Value = Mathf.Max(0, this.predators.Value + this.predators.Value * s * sqrtH * gaussianNoise());

        if (this.withInvader.Value)
        {
            this.invaders.Value = Mathf.Max(0, this.invaders.Value + this.invaders.Value * s * sqrtH * gaussianNoise());
        }
    }

    /// <summary>
    /// Aplica el impacto del fuego a las poblaciones cuando `fireActive` está activo.
    /// La tasa base de mortalidad por segundo se multiplica por el factor `fireIntensity / 50`.
    /// Se aplican como decrementos proporcionales a cada población y escalados por `deltaT`.
    /// </summary>
    private void applyFireImpact()
    {
        if (!this.fireActive.Value) return;
        if (!IsServer) return; // Solo el servidor debe actualizar NetworkVariables

        // Intensidad normal = 50 -> factor 1.0. A 100 -> 2.0, a 25 -> 0.5
        float intensity = Mathf.Max(0, this.fireIntensity.Value);
        float scale = intensity / 50f;

        // Tasas base de mortalidad por segundo (puedes ajustarlas según convenga)
        float basePreyMortality = 0.5f;   // 50% de presas
        float basePredMortality = 0.2f;   // 20% de depredadores
        float baseInvMortality = 0.3f;    // 30% de invasore

        float dt = this.deltaT.Value;

        float preyMort = basePreyMortality * scale;
        float predMort = basePredMortality * scale;
        float invMort = baseInvMortality * scale;

        this.preys.Value = Mathf.Max(0f, this.preys.Value * (1f - preyMort));
        this.predators.Value = Mathf.Max(0f, this.predators.Value * (1f - predMort));

        if (this.withInvader.Value)
        {
            this.invaders.Value = Mathf.Max(0f, this.invaders.Value * (1f - invMort));
        }

        if (this.loadActive.Value)
        {
            // Reducimos la capacidad de carga proporcionalmente a la intensidad.
            // A intensidad máxima (100), el entorno solo soporta el 20% de lo habitual.
            float loadImpact = Mathf.Clamp01(0.8f * scale);
            this.load.Value = Mathf.RoundToInt(this.load.Value * (1f - loadImpact));
        }

        Debug.Log($"[FireImpact] intensity={intensity} scale={scale:F2} preyMort={preyMort:F3} predMort={predMort:F3} invMort={invMort:F3}");
    }

    // --- Numerical methods ---

    public void Rk4LotkaVolterra(
        float x, float y,
        float alpha, float beta,
        float delta, float gamma,
        float h)
    {
        float dx(float x, float y)
        {
            float growth = alpha * x * getCarryingCapacityFactor(x);
            return growth - beta * x * y;
        }

        float dy(float x, float y) => delta * x * y - gamma * y;

        float k1x = dx(x, y);
        float k1y = dy(x, y);

        float k2x = dx(x + 0.5f * h * k1x, y + 0.5f * h * k1y);
        float k2y = dy(x + 0.5f * h * k1x, y + 0.5f * h * k1y);

        float k3x = dx(x + 0.5f * h * k2x, y + 0.5f * h * k2y);
        float k3y = dy(x + 0.5f * h * k2x, y + 0.5f * h * k2y);

        float k4x = dx(x + h * k3x, y + h * k3y);
        float k4y = dy(x + h * k3x, y + h * k3y);

        this.preys.Value = Mathf.Max(0, x + (h / 6.0f) * (k1x + 2 * k2x + 2 * k3x + k4x));
        this.predators.Value = Mathf.Max(0, y + (h / 6.0f) * (k1y + 2 * k2y + 2 * k3y + k4y));
    }

    public void Rk4LotkaVolterraInvader(
        float x, float y, float z,
        float alpha, float beta,
        float delta, float gamma,
        float eta, float zeta,
        float epsilon, float omega,
        float h)
    {
        float dx(float x, float y, float z)
        {
            float growth = alpha * x * getCarryingCapacityFactor(x);
            return growth - beta * x * y - epsilon * x * z;
        }

        float dy(float x, float y, float z) =>
            delta * x * y + delta * zeta * z * y - gamma * y;

        float dz(float x, float y, float z)
        {
            return eta * x * z - omega * x * z - zeta * z * y;
        }

        float k1x = dx(x, y, z);
        float k1y = dy(x, y, z);
        float k1z = dz(x, y, z);

        float k2x = dx(x + 0.5f * h * k1x, y + 0.5f * h * k1y, z + 0.5f * h * k1z);
        float k2y = dy(x + 0.5f * h * k1x, y + 0.5f * h * k1y, z + 0.5f * h * k1z);
        float k2z = dz(x + 0.5f * h * k1x, y + 0.5f * h * k1y, z + 0.5f * h * k1z);

        float k3x = dx(x + 0.5f * h * k2x, y + 0.5f * h * k2y, z + 0.5f * h * k2z);
        float k3y = dy(x + 0.5f * h * k2x, y + 0.5f * h * k2y, z + 0.5f * h * k2z);
        float k3z = dz(x + 0.5f * h * k2x, y + 0.5f * h * k2y, z + 0.5f * h * k2z);

        float k4x = dx(x + h * k3x, y + h * k3y, z + h * k3z);
        float k4y = dy(x + h * k3x, y + h * k3y, z + h * k3z);
        float k4z = dz(x + h * k3x, y + h * k3y, z + h * k3z);

        this.preys.Value = Mathf.Max(0, x + (h / 6.0f) * (k1x + 2 * k2x + 2 * k3x + k4x));
        this.predators.Value = Mathf.Max(0, y + (h / 6.0f) * (k1y + 2 * k2y + 2 * k3y + k4y));
        this.invaders.Value = Mathf.Max(0, z + (h / 6.0f) * (k1z + 2 * k2z + 2 * k3z + k4z));
    }

    private void RK4Iteration()
    {
        if (this.withInvader.Value)
        {
            Rk4LotkaVolterraInvader(
                this.preys.Value, this.predators.Value, this.invaders.Value,
                this.alpha.Value, this.beta.Value, this.delta.Value, this.gamma.Value,
                this.eta.Value, this.zeta.Value, this.epsilon.Value,
                this.omega.Value, this.deltaT.Value);
        }
        else
        {
            Rk4LotkaVolterra(
                this.preys.Value, this.predators.Value,
                this.alpha.Value, this.beta.Value, this.delta.Value,
                this.gamma.Value, this.deltaT.Value);
        }
    }

    private void eulerIteration()
    {
        float x = this.preys.Value;
        float y = this.predators.Value;
        float z = this.invaders.Value;
        float dt = this.deltaT.Value;

        float dPrey;
        float dPred;
        float dInv = 0;

        if (!this.withInvader.Value)
        {
            // Modelo 2 especies
            float growthTerm = this.alpha.Value * x * getCarryingCapacityFactor(x);

            dPrey = growthTerm - (this.beta.Value * x * y);
            dPred = (this.delta.Value * x * y) - (this.gamma.Value * y);
        }
        else
        {
            // Modelo 3 especies (Presa, Depredador, Invasor)
            float growthPrey = this.alpha.Value * x * getCarryingCapacityFactor(x);

            dPrey = growthPrey - (this.beta.Value * x * y) - (this.epsilon.Value * x * z);

            dPred = (this.delta.Value * x * y)
                    - (this.gamma.Value * y)
                    - (this.omega.Value * y * z);

            dInv = (this.eta.Value * x * z)
                - (this.zeta.Value * z)
                - (this.omega.Value * y * z);
        }

        // Integración de Euler con clamp a 0
        this.preys.Value = Mathf.Max(0, x + dPrey * dt);
        this.predators.Value = Mathf.Max(0, y + dPred * dt);

        if (this.withInvader.Value)
        {
            this.invaders.Value = Mathf.Max(0, z + dInv * dt);
        }
    }

    // --- Main simulation step ---

    private void runVolterraIteration()
    {
        // Execute the selected numerical method
        if (this.equation.Value == 0)
            eulerIteration();
        else if (this.equation.Value == 1)
            RK4Iteration();

        // Apply stochastic noise after the deterministic step
        applyStochasticNoise(this.deltaT.Value);

        // Apply fire impact if active (server-side)
        applyFireImpact();

        Debug.Log($"Iteración: Pred={this.predators.Value:F2}  Prey={this.preys.Value:F2}  Inv={this.invaders.Value:F2}  dt={this.deltaT.Value}");

        // Update graphs
        addPoint();

        // Send log
        if (this.withInvader.Value)
            logSender.sendPopulationUpdate(Mathf.CeilToInt(this.preys.Value), Mathf.CeilToInt(this.predators.Value), Mathf.CeilToInt(this.invaders.Value));
        else
            logSender.sendPopulationUpdate(Mathf.CeilToInt(this.preys.Value), Mathf.CeilToInt(this.predators.Value));

        // Update 3D visual models
        updateSceneVisuals();
    }

    /// <summary>
    /// Decides whether to normalize (when total > MAX_INSTANCES) or use raw values,
    /// then updates predator/prey/invader 3D models accordingly.
    /// </summary>
    private void updateSceneVisuals()
    {
        float totalPopulation = this.withInvader.Value
            ? this.preys.Value + this.predators.Value + this.invaders.Value
            : this.preys.Value + this.predators.Value;

        bool needsNormalization = totalPopulation > MAX_INSTANCES;

        int newPredScene = needsNormalization
            ? calculateNormalizedVisual(this.predators.Value)
            : Mathf.CeilToInt(this.predators.Value);

        int newPreyScene = needsNormalization
            ? calculateNormalizedVisual(this.preys.Value)
            : Mathf.CeilToInt(this.preys.Value);

        updateVisualModels(this.predatorsScene.Value, newPredScene, this.preysScene.Value, newPreyScene);
        this.predatorsScene.Value = newPredScene;
        this.preysScene.Value = newPreyScene;

        if (this.withInvader.Value)
        {
            int newInvScene = needsNormalization
                ? calculateNormalizedVisual(this.invaders.Value)
                : Mathf.CeilToInt(this.invaders.Value);

            updateInvaderVisual(this.invadersScene.Value, newInvScene);
            this.invadersScene.Value = newInvScene;
        }
    }
}
