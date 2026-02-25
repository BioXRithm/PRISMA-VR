using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AdvancedConfigurationController : MonoBehaviour
{
    [Header("Toggles")]
    public Toggle loadToggle;
    public Toggle stochasticToggle;
    public Toggle fireToggle;

    [Header("Sliders and Input field")]
    [SerializeField] private TMP_InputField loadInput;
    [SerializeField] private TMP_InputField stochasticInput;
    [SerializeField] private Slider fireInput;

    private void Start()
    {
        if (loadToggle != null)
            loadToggle.onValueChanged.AddListener(OnLoadChanged);
        else
            Debug.LogError("Load toggle is not assigned!");

        if (stochasticToggle != null)
            stochasticToggle.onValueChanged.AddListener(OnStochasticChanged);
        else
            Debug.LogError("Stochastic toggle is not assigned!");

        if (fireToggle != null)
            fireToggle.onValueChanged.AddListener(OnFireChanged);
        else
            Debug.LogError("Fire toggle is not assigned!");
    }

    private void OnDestroy()
    {
        if (loadToggle != null)
            loadToggle.onValueChanged.RemoveListener(OnLoadChanged);

        if (stochasticToggle != null)
            stochasticToggle.onValueChanged.RemoveListener(OnStochasticChanged);
    }

    public void OnLoadChanged(bool isActive)
    {
        if (SimulationNetworkManager.Instance == null)
        {
            Debug.LogError("SimulationNetworkManager.Instance es null");
            return;
        }

        int load = 0;
        if (isActive && loadInput != null)
            int.TryParse(loadInput.text, out load);

        SimulationNetworkManager.Instance.RequestChangeLoad(isActive, load);
    }

    public void OnStochasticChanged(bool isActive)
    {
        if (SimulationNetworkManager.Instance == null)
        {
            Debug.LogError("SimulationNetworkManager.Instance es null");
            return;
        }

        float sigma = 0f;
        if (isActive && stochasticInput != null)
            float.TryParse(stochasticInput.text, out sigma);

        SimulationNetworkManager.Instance.RequestChangeStochastic(isActive, sigma);
    }

    public void OnFireChanged(bool isActive)
    {
        if (SimulationNetworkManager.Instance == null)
        {
            Debug.LogError("SimulationNetworkManager.Instance es null");
            return;
        }

        int intensity = 0;
        if (isActive && fireInput != null)
            intensity = (int)fireInput.value;

        SimulationNetworkManager.Instance.RequestChangeFire(isActive, intensity);
    }
}
