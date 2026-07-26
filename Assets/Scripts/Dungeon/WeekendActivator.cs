using System;
using UnityEngine;

/// <summary>
/// Enables/disables target GameObjects based on the current day of week,
/// using local system time. Attach this to an always-active manager object
/// (e.g. "EventManager") so it keeps running even when targets are off.
/// </summary>
public class WeekendActivator : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Objects to enable/disable based on the active days below.")]
    [SerializeField] private GameObject[] targets;

    [Header("Active Days")]
    [Tooltip("Targets will be active only on the checked days.")]
    [SerializeField] private bool sunday = true;
    [SerializeField] private bool monday = false;
    [SerializeField] private bool tuesday = false;
    [SerializeField] private bool wednesday = false;
    [SerializeField] private bool thursday = false;
    [SerializeField] private bool friday = false;
    [SerializeField] private bool saturday = true;

    [Header("Settings")]
    [Tooltip("How often (in seconds) to re-check the day of week while the app is running.")]
    [SerializeField] private float recheckIntervalSeconds = 60f;

    private float _timer;

    private void OnEnable()
    {
        ApplyActiveState();
        _timer = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= recheckIntervalSeconds)
        {
            _timer = 0f;
            ApplyActiveState();
        }
    }

    private void ApplyActiveState()
    {
        bool shouldBeActive = IsTodayActiveDay();

        foreach (var target in targets)
        {
            if (target == null) continue;

            if (target.activeSelf != shouldBeActive)
            {
                target.SetActive(shouldBeActive);
            }
        }
    }

    private bool IsTodayActiveDay()
    {
        switch (DateTime.Now.DayOfWeek)
        {
            case DayOfWeek.Sunday: return sunday;
            case DayOfWeek.Monday: return monday;
            case DayOfWeek.Tuesday: return tuesday;
            case DayOfWeek.Wednesday: return wednesday;
            case DayOfWeek.Thursday: return thursday;
            case DayOfWeek.Friday: return friday;
            case DayOfWeek.Saturday: return saturday;
            default: return false;
        }
    }

    [ContextMenu("Force Re-check")]
    public void ForceRecheck()
    {
        ApplyActiveState();
    }
}