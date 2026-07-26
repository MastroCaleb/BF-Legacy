using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PulseObjects : MonoBehaviour
{
    public float cycleDuration = 1f;
    public List<GameObject> objs;
    private float timer = 0f;
    private int currentObjIndex = 0;

    void Update()
    {
        if (cycleDuration <= 0f) return;
        if (ActiveCount() <= 1) return;

        timer += Time.deltaTime;

        float normalizedTime = timer / (cycleDuration * 0.5f);
        float t = Mathf.PingPong(normalizedTime, 1f);

        SetColor(objs[currentObjIndex], t);

        int prevFloor = Mathf.FloorToInt((timer - Time.deltaTime) / (cycleDuration * 0.5f));
        int currFloor = Mathf.FloorToInt(normalizedTime);

        if (currFloor != prevFloor && currFloor % 2 == 0 && currFloor > 0)
        {
            SetColor(objs[currentObjIndex], 0f);
            currentObjIndex = (currentObjIndex + 1) % objs.Count;
            timer = 0f;
        }
    }

    int ActiveCount()
    {
        int count = 0;
        foreach (GameObject obj in objs)
            if (obj != null && obj.activeInHierarchy) count++;
        return count;
    }

    void SetColor(GameObject obj, float t)
    {
        Image img = obj.GetComponent<Image>();
        if(img != null){
            Color c = img.color;
            c.a = t;
            img.color = c;
        }

        TextMeshProUGUI text = obj.GetComponentInChildren<TextMeshProUGUI>();
        if(text != null){
            Color c = text.color;
            c.a = t;
            text.color = c;
        }
    }

    void OnEnable()
    {
        ResetPulse();
    }

    public void ResetPulse()
    {
        timer = 0f;
        currentObjIndex = 0;

        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null)
                SetColor(objs[i], i == 0 ? 1f : 0f);
    }
}