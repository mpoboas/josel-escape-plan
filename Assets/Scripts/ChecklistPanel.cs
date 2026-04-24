using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Real-time HUD panel that displays gameplay statistics and safety progress.
/// Mirroring the logic used in the EndPanelController.
/// </summary>
public class ChecklistPanel : MonoBehaviour
{
    [Header("UI Text References")]
    [Tooltip("Displays accumulated smoke damage / max allowed.")]
    public TMP_Text smokeDamageText;

    [Tooltip("Displays accumulated fire damage / max allowed.")]
    public TMP_Text fireDamageText;

    [Tooltip("Displays doors currently closed / required closed.")]
    public TMP_Text doorsClosedText;

    [Tooltip("Displays doors heat-checked / required checked.")]
    public TMP_Text doorsCheckedText;

    [Header("Settings")]
    [Tooltip("How many times per second the UI updates.")]
    [Range(0.1f, 2f)]
    public float refreshInterval = 0.5f;

    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= refreshInterval)
        {
            _timer = 0f;
            RefreshUI();
        }
    }

    private void RefreshUI()
    {
        var stats = GameplaySessionStats.Instance;
        if (stats == null) return;

        // 1. Get Requirements from GameManager
        var gm = FindAnyObjectByType<GameManager>();
        float maxSmoke = 0f;
        float maxFire = 0f;
        int reqClosed = 0;
        int reqChecked = 0;

        if (gm != null && gm.levels != null)
        {
            int currentLevelIndex = PlayerPrefs.GetInt("SelectedLevel", 0);
            if (currentLevelIndex >= 0 && currentLevelIndex < gm.levels.Length)
            {
                var level = gm.levels[currentLevelIndex];
                maxSmoke = level.maxSmokeDamageAllowed;
                maxFire = level.maxFireDamageAllowed;
                reqClosed = level.minDoorsClosedRequired;
                reqChecked = level.minDoorsCheckedRequired;
            }
        }

        // 2. Damage
        if (smokeDamageText != null) 
            smokeDamageText.text = $"{Mathf.RoundToInt(stats.SmokeDamageTaken)} / {Mathf.RoundToInt(maxSmoke)}";

        if (fireDamageText != null) 
            fireDamageText.text = $"{Mathf.RoundToInt(stats.FireDamageTaken)} / {Mathf.RoundToInt(maxFire)}";

        // 3. Door Statistics Logic
        int totalChecked = stats.HeatCheckedDoorCount;
        int currentlyClosedThatWereOpened = 0;

        var allDoors = FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var openedIds = stats.OpenedDoorIdsSet;

        foreach (var door in allDoors)
        {
            if (door != null && openedIds.Contains(door.GetInstanceID()))
            {
                if (!door.IsOpen)
                {
                    currentlyClosedThatWereOpened++;
                }
            }
        }

        // 4. Display strings in "Current / Required" format
        if (doorsClosedText != null)
            doorsClosedText.text = $"{currentlyClosedThatWereOpened} / {reqClosed}";

        if (doorsCheckedText != null)
            doorsCheckedText.text = $"{totalChecked} / {reqChecked}";
    }
}
