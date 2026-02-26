using UnityEngine;
using TMPro;
using UnityEngine.UI; 

public class UpdateSimulation : MonoBehaviour
{
    public Slider alphaSlider, betaSlider, gammaSlider, deltaSlider;
    public TMP_InputField timeField, preyField, predatorField;
    public Toggle submitButton; // UI Set button (To submit values)
    private VolterraStatus volterra;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if ((submitButton != null) && (alphaSlider != null) && (betaSlider != null) && (gammaSlider != null) && (deltaSlider != null))
        {
            submitButton.onValueChanged.AddListener(OnSubmitButtonToggled);
        }
        else
        {
            Debug.LogError("Submit button (Toggle) is not assigned!");
        }

        GameObject go = GameObject.Find("VolterraStatus");
        volterra = go.GetComponent<VolterraStatus>();
    }

    void OnSubmitButtonToggled(bool isOn)
    {
        Debug.Log("EVENT LANZADO -------");

            if ((alphaSlider != null) && (betaSlider != null) && (gammaSlider != null) && (deltaSlider != null))
            {
                float.TryParse(timeField.text, out float deltaTime);
                int.TryParse(predatorField.text, out int predators);
                int.TryParse(preyField.text, out int preys);
                
                float alpha = alphaSlider.value;
                float beta = betaSlider.value;
                float gamma = gammaSlider.value;
                float delta = deltaSlider.value;
                Debug.Log("Slider: " + alpha + "-" + beta + "-" + gamma + "-" + delta);
                
                volterra.updateValuesServerRpc(alpha, beta, gamma, delta, deltaTime, preys, predators);
                
            }
            else
            {
                Debug.LogError("Input Field is not assigned!");
            }
        
    }
}
