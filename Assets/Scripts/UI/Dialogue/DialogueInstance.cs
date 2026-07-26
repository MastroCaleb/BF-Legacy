using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueInstance : MonoBehaviour
{
    public TextMeshProUGUI textObj;
    public bool isWriting;
    public GameObject nextLineImage;
    string currentText;
    Coroutine c = null;
    public Dialogue dialogue;
    public bool completed;
    int currentLineId = 0;
    bool lastLineComplete;
    Button button;

    void Start()
    {
        NextLine();
        button = GetComponent<Button>();
        button.onClick.AddListener(NextLine);
    }

    void NextLine(){
        if(dialogue != null && dialogue.lines.Count > 0 && !completed){
            
            if(currentLineId < dialogue.lines.Count){
                //Add name if needed
                bool nextLine = Type(dialogue.lines[currentLineId]);
                if(nextLine) currentLineId++;
            }
            else if(currentLineId == dialogue.lines.Count && !lastLineComplete){
                //Add name if needed
                CompleteWriting(dialogue.lines[currentLineId-1]);
                lastLineComplete = true;
            }
            
            if(currentLineId == dialogue.lines.Count && lastLineComplete){
                Clear();
                currentLineId = 0;
                completed = true;
            }
        }
        else{
            Debug.Log("Yo you need the dialogue maybe?");
        }
    }

    void Update()
    {
        if (nextLineImage != null)
        {
            nextLineImage.SetActive(!isWriting);
        }
    }

    public bool Type(string newText)
    {
        // If there's an ongoing typing coroutine, stop it and complete the current text
        if (c != null)
        {
            CompleteWriting(currentText); // Display the entire line currently being typed
            return false;
        }
        else{
            // Clear current text and set up for new text
            currentText = newText; // Set new text
            Clear(); // Reset the text on screen
            
            // Start typing coroutine
            c = StartCoroutine(Write(newText));
            return true;
        }
    }

    IEnumerator Write(string text)
    {
        isWriting = true;
        bool insideTag = false;

        text = ExecuteCommands(text);

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<') insideTag = true;
            
            textObj.text += text[i];

            if (text[i] == '>') insideTag = false;

            if (!insideTag)
                yield return new WaitForSeconds(0.1f);
        }

        isWriting = false;
        c = null;
    }

    public string ExecuteCommands(string text)
    {
        if(text.Replace("<playername>", PlayerData.playerName) != text){
            text = text.Replace("<playername>", PlayerData.playerName);
        }
        return text;
    }

    public void CompleteWriting(string text)
    {
        if (c == null) return;
        StopCoroutine(c);
        textObj.text = ExecuteCommands(text); // Display the entire current line
        isWriting = false;
        c = null;
    }

    public void Clear()
    {
        textObj.text = "";
        isWriting = false;
    }

    public void SetDialogue(Dialogue newDialogue){
        dialogue = newDialogue;
        currentLineId = 0;
        completed = false;
        lastLineComplete = false;
        NextLine();
    }
}
