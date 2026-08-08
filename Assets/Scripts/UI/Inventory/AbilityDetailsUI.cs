using TMPro;
using UnityEngine;

public class AbilityDetailsUI : MonoBehaviour
{
    public static Unit unit;

    public GameObject lsLabel;
    public TextMeshProUGUI lsName;
    public TextMeshProUGUI lsDesc;

    public GameObject bbLabel;
    public TextMeshProUGUI bbName;
    public TextMeshProUGUI bbDesc;

    public GameObject sbbLabel;
    public TextMeshProUGUI sbbName;
    public TextMeshProUGUI sbbDesc;

    public GameObject ubbLabel;
    public TextMeshProUGUI ubbName;
    public TextMeshProUGUI ubbDesc;

    public GameObject exLabel;
    public TextMeshProUGUI exName;
    public TextMeshProUGUI exDesc;

    public bool isBattle;

    //Not implemented
    public GameObject ex2Label;
    public TextMeshProUGUI ex2Name;
    public TextMeshProUGUI ex2Desc;

    public void OnEnable()
    {
        if(unit == null) return;

        if(unit.leaderAbility != null)
        {
            lsLabel.SetActive(isBattle ? false : true);
            lsName.text = unit.leaderAbility.abilityName;
            lsDesc.text = unit.leaderAbility.abilityDesc;
        }
        else
        {
            lsLabel.SetActive(isBattle ? false : true);
            lsName.text = "None";
            lsDesc.text = "";
        }

        if(unit.bbAbility != null)
        {
            bbLabel.SetActive(true);
            bbName.text = unit.bbAbility.abilityName;
            bbDesc.text = unit.bbAbility.abilityDesc;
        }
        else
        {
            bbLabel.SetActive(true);
            bbName.text = "None";
            bbDesc.text = "";
        }

        if(unit.sbbAbility != null)
        {
            sbbLabel.SetActive(true);
            sbbName.text = unit.sbbAbility.abilityName;
            sbbDesc.text = unit.sbbAbility.abilityDesc;
        }
        else
        {
            sbbLabel.SetActive(false);
        }

        if(unit.ubbAbility != null)
        {
            ubbLabel.SetActive(true);
            ubbName.text = unit.ubbAbility.abilityName;
            ubbDesc.text = unit.ubbAbility.abilityDesc;
        }
        else
        {
            ubbLabel.SetActive(false);
        }

        if(unit.extraAbility != null)
        {
            exLabel.SetActive(isBattle ? false : true);
            exName.text = unit.extraAbility.abilityName;
            exDesc.text = unit.extraAbility.abilityDesc;
        }
        else
        {
            exLabel.SetActive(false);
        }
    }

    void OnDisable()
    {
        gameObject.SetActive(false);
    }
}