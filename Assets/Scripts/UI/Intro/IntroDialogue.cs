using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class IntroDialogue : MonoBehaviour
{
    public DialogueInstance dialogueInstance;

    public Dialogue dialogue_1; //Shouldnt actually use as its already set in the instance
    public Dialogue dialogue_2;
    public Dialogue dialogue_3;

    public GameObject lucius_1;
    public GameObject lucius_2;
    public GameObject lucius_3;

    public GameObject nameTextField;
    public GameObject heroSelection;

    public Image fade;

    // Added
    private bool transitioning = false;

    void Start()
    {
        StartCoroutine(FadeToTransparent());
        StartCoroutine(WaitToStart());
        // Start fully transparent
        Color c = fade.color;
        c.a = 0f;
        fade.color = c;
    }

    IEnumerator WaitToStart()
    {
        yield return new WaitForSeconds(0.25f);
        dialogueInstance.SetDialogue(dialogue_1);
    }

    void Update()
    {
        if(dialogueInstance.dialogue == dialogue_1 && dialogueInstance.completed)
        {
            nameTextField.SetActive(true);
        }
        else if(dialogueInstance.dialogue == dialogue_2 && dialogueInstance.completed)
        {
            heroSelection.SetActive(true);
        }
        else if(dialogueInstance.dialogue == dialogue_3 && dialogueInstance.completed && !transitioning)
        {
            List<string> possibleSlimeUnits = new List<string>
            {
                "10030", "20030", "30030",
                "40030", "50030", "60030"
            };

            for(int i = 0; i < 2; i++)
            {
                string randomUnitId = possibleSlimeUnits[Random.Range(0, possibleSlimeUnits.Count)];
                int unitKey = PlayerUnitInventoryDatabase.AddUnit(UnitRegistry.GetUnitById(randomUnitId), isNew: true);
                PartyDatabase.GetParty(0).SetUnitAt(i + 1, unitKey);
            }

            StartCoroutine(FadeToWhiteAndLoadScene());
        }
    }

    public void SetName()
    {
        PlayerData.completedMissionDex.Add("2");
        PlayerData.presentCollectedDex.Add("FirstLoginGemGift");
        PlayerData.playerName = nameTextField.GetComponentInChildren<TMPro.TMP_InputField>().text;
        dialogueInstance.SetDialogue(dialogue_2);
        nameTextField.SetActive(false);
        lucius_1.SetActive(false);
        lucius_2.SetActive(true);
    }

    public void SetHero()
    {
        int unitKey = PlayerUnitInventoryDatabase.AddUnit(heroSelection.GetComponent<SelectStartHero>().GetSelectedHero(), isNew: true);
        PartyDatabase.CreateParty();
        PartyDatabase.GetParty(0).SetUnitAt(0, unitKey);
        dialogueInstance.SetDialogue(dialogue_3);
        heroSelection.SetActive(false);
        lucius_2.SetActive(false);
        lucius_3.SetActive(true);
    }

    // Added
    IEnumerator FadeToWhiteAndLoadScene()
    {
        transitioning = true;

        fade.gameObject.SetActive(true);

        float duration = 3f;
        float elapsed = 0f;

        Color color = Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Clamp01(elapsed / duration);
            fade.color = color;
            yield return null;
        }

        PlayerData.SaveDataToJson();
        SceneManager.LoadScene("MainMenuScene");
    }

    IEnumerator FadeToTransparent()
    {
        transitioning = true;
        fade.gameObject.SetActive(true);

        float duration = 15f;
        float elapsed = 0f;
        Color color = Color.black;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Clamp01(1f - (elapsed / duration));
            fade.color = color;
            yield return null;
        }
        color.a = 0f;
        fade.color = color;
        fade.gameObject.SetActive(false);
        
        transitioning = false;
    }

}
