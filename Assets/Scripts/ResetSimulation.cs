using UnityEngine;
using UnityEngine.UI; 

public class ResetSimulation : MonoBehaviour
{
    public StartSimulation startSimulation;
    public Toggle submitButton; // UI Set button (To submit values)
    public AddInvasor invaderButton;
    private VolterraStatus volterra;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (submitButton != null)
        {
            submitButton.onValueChanged.AddListener(OnSubmitButtonToggled);
        }
        else
        {
            Debug.LogError("Submit button (Reset) is not assigned!");
        }
        GameObject go = GameObject.Find("VolterraStatus");
        volterra = go.GetComponent<VolterraStatus>();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnSubmitButtonToggled(bool isOn) {
        volterra.stopSimulation();
        volterra.resetGraph();
        startSimulation.firstTime = true;
        startSimulation.simRunning = false;
        startSimulation.destroyCurrentModels();
        if (volterra.withInvader.Value)
        {
            invaderButton.OnSubmitButtonToggled(true);
        }

        
        startSimulation.OnSubmitButtonToggled(true);
    }
}
