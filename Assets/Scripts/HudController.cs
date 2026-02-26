using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    public GameObject hudNumbers;
    public TextMeshProUGUI wolvesText;
    public TextMeshProUGUI rabbitsText;
    public TextMeshProUGUI invadersText;

    private float preys;
    private float predators;
    private float invaders;
    private VolterraStatus volterraStatus;


    void Start()
    {
        rabbitsText.text = $"{10}";
        wolvesText.text = $"{3}";
        invadersText.text = $"{3}";
        GameObject go = GameObject.Find("VolterraStatus");
        volterraStatus = go.GetComponent<VolterraStatus>();
        volterraStatus.preys.OnValueChanged += onPreysChanged;
        volterraStatus.predators.OnValueChanged += onPredatorsChanged;
        volterraStatus.invaders.OnValueChanged += onInvadersChanged;
        volterraStatus.withInvader.OnValueChanged += onAddedInvaders;
    }

    void OnDestroy()
    {
        if (volterraStatus != null)
        {
            volterraStatus.preys.OnValueChanged -= onPreysChanged;
            volterraStatus.predators.OnValueChanged -= onPredatorsChanged;
            volterraStatus.invaders.OnValueChanged -= onInvadersChanged;
            volterraStatus.withInvader.OnValueChanged -= onAddedInvaders;
        }
    }

    void onPredatorsChanged(float oldValue, float predators)
    {
        wolvesText.text = $"{Mathf.CeilToInt(predators)}";
        this.predators = predators;
    }
    void onInvadersChanged(float oldValue, float invaders)
    {
        invadersText.text = $"{Mathf.CeilToInt(invaders)}";
        this.invaders = invaders;
    }
     void onPreysChanged(float oldValue, float preys)
    {
        rabbitsText.text = $"{Mathf.CeilToInt(preys)}";
        this.preys = preys;
    }

    public void toggleHud() {
        hudNumbers.SetActive(!hudNumbers.activeSelf);
    }

    private void onAddedInvaders(bool previousValue, bool newValue)
    {
        hudNumbers.transform.Find("InvaderDisplay").gameObject.SetActive(volterraStatus.withInvader.Value);
        hudNumbers.transform.Find("Invaders").gameObject.SetActive(volterraStatus.withInvader.Value);
    }
}