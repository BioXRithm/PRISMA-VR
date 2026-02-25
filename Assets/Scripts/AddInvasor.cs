using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AddInvasor : MonoBehaviour
{
    public TMP_InputField invader;
    public Slider etaSlider, zetaSlider, epsilonSlider, omegaSlider;
    public Toggle submitButton;

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

    public void OnSubmitButtonToggled(bool isOn)
    {
        Debug.LogError($"Añadir PULSADO: {isOn}");

        int.TryParse(invader.text, out int invaders);
        float eta = etaSlider.value;
        float zeta = zetaSlider.value;
        float epsilon = epsilonSlider.value;
        float omega = omegaSlider.value;

        Debug.Log($"🚀 Solicitando añadir invasor al servidor");
        
        if (SimulationNetworkManager.Instance != null)
        {
            SimulationNetworkManager.Instance.RequestAddInvader(
                invaders, eta, zeta, epsilon, omega);
        }
        else
        {
            Debug.LogError("❌ SimulationNetworkManager.Instance es null");
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


}
