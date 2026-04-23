using UnityEngine;
using TMPro;

/// <summary>
/// Updates a UI text component with the current session playtime.
/// </summary>
public class TimerPanel : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("TextMeshPro text component where the timer will be displayed.")]
    [SerializeField] private TMP_Text timerText;

    private void Update()
    {
        // Early exit if stats aren't initialized or reference is missing
        if (GameplaySessionStats.Instance == null || timerText == null)
            return;

        float elapsed = GameplaySessionStats.Instance.ElapsedSeconds;
        timerText.text = FormatTime(elapsed);
    }

    /// <summary>
    /// Formats seconds into MM:SS format.
    /// </summary>
    private string FormatTime(float totalSeconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(totalSeconds));
        int mins = total / 60;
        int secs = total % 60;
        
        // formats into 00:00 style
        return string.Format("{0:00}:{1:00}", mins, secs);
    }
}
