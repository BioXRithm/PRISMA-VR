using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class StartSimulation : MonoBehaviour
{
    public TMP_InputField depredadores, presas, time;
    public Slider alphaSlider, betaSlider, gammaSlider, deltaSlider;
    public Toggle submitButton;

    public ToggleGroup dropdownToggleGroup;

    public bool simRunning = false;
    public bool firstTime = true;

    void Start()
    {
        Debug.Log("StartSimulation - Inicia");
        if (submitButton != null)
        {
            submitButton.onValueChanged.AddListener(OnSubmitButtonToggled);
        }
        else
        {
            Debug.LogError("Submit button (Toggle) is not assigned!");
        }
    }

    public int getEquationValue()
    {
        Toggle activeToggle = dropdownToggleGroup.ActiveToggles().FirstOrDefault();

        if (activeToggle != null)
        {
            TMP_Text textComponent = activeToggle.GetComponentInChildren<TMP_Text>();
            string selectedText = textComponent.text;
           
            int eq = 0;
            if (selectedText == "Euler")
            {
                eq = 0;
            } else if (selectedText == "RK4")
            {
                eq = 1;
            }
            Debug.Log("-----------------------------------------------El texto es: " + eq);
            return eq;
        }
        else
        {
            Debug.LogWarning("No hay ninguna opción seleccionada en el Dropdown.");
        }
        return -1;
    }

    public void OnSubmitButtonToggled(bool isOn)
    {
        Debug.Log($"Iniciar PULSADO: {isOn}");

        if (!simRunning)
        {
            simRunning = true;
            if ((depredadores != null) && (presas != null))
            {
                int.TryParse(depredadores.text, out int predators);
                int.TryParse(presas.text, out int preys);
                float.TryParse(time.text, out float deltaTime);
                float alpha = alphaSlider.value;
                float beta = betaSlider.value;
                float gamma = gammaSlider.value;
                float delta = deltaSlider.value;
                int equation = getEquationValue();


                Debug.Log($"🚀 Solicitando iniciar simulación al servidor");
                
                if (SimulationNetworkManager.Instance != null)
                {
                    SimulationNetworkManager.Instance.RequestStartSimulation(
                        predators, preys, alpha, beta, gamma, delta, firstTime, deltaTime, equation);
                }
                else
                {
                    Debug.LogError("❌ SimulationNetworkManager.Instance es null");
                }
            }
            else
            {
                Debug.LogError("Input fields no asignados");
            }
        }
        else
        {
            Debug.Log("🚀 Solicitando detener simulación");
            
            if (SimulationNetworkManager.Instance != null)
            {
                SimulationNetworkManager.Instance.RequestStopSimulation();
            }
        }
    }

    public void destroyCurrentModels()
    {
        Debug.Log("🗑 Solicitando destruir modelos");
        if (SimulationNetworkManager.Instance != null)
        {
            SimulationNetworkManager.Instance.RequestDestroyModels();
        }
        else
        {
            Debug.LogError("❌ SimulationNetworkManager.Instance es null");
        }
    }

    public void OnSimulationStarted()
    {
        simRunning = true;
        firstTime = false;
        
        GameObject toggleObject = submitButton.gameObject;
        TMP_Text labelTextComponent = toggleObject.GetComponentInChildren<TMP_Text>();
        if (labelTextComponent != null)
        {
            labelTextComponent.text = "Detener";
        }
        
        Debug.Log("✓ UI actualizada: Simulación iniciada");
    }

    public void OnSimulationStopped()
    {
        simRunning = false;
        
        GameObject toggleObject = submitButton.gameObject;
        TMP_Text labelTextComponent = toggleObject.GetComponentInChildren<TMP_Text>();
        if (labelTextComponent != null)
        {
            labelTextComponent.text = "Iniciar";
        }
        
        Debug.Log("✓ UI actualizada: Simulación detenida");
    }
}
