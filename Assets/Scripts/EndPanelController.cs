using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the final statistics panel, allowing the player to navigate to the next level,
/// restart the current one, or return to the main menu.
/// </summary>
public class EndPanelController : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Name of the main menu scene.")]
    public string menuSceneName = "MainMenu";
    
    [Tooltip("Name of the game scene (where GameManager logic resides).")]
    public string gameSceneName = "B";

    private void Awake()
    {
        // Ensures the panel starts hidden when the game runs
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Call this method to display the end panel, pause the game, and unlock the cursor.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;

        // Unlock and show cursor for button interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[EndPanel] Panel displayed.");
    }

    /// <summary>
    /// Increments the SelectedLevel in PlayerPrefs and reloads the game scene.
    /// The GameManager will automatically load the new building configuration.
    /// </summary>
    public void NextLevel()
    {
        int currentLevel = PlayerPrefs.GetInt("SelectedLevel", 0);
        PlayerPrefs.SetInt("SelectedLevel", currentLevel + 1);
        PlayerPrefs.Save();

        Debug.Log($"[EndPanel] Advancing to level index: {currentLevel + 1}");
        ReloadGameScene();
    }

    /// <summary>
    /// Reloads the current scene to restart the level from the beginning.
    /// </summary>
    public void RestartLevel()
    {
        Debug.Log("[EndPanel] Restarting current level.");
        ReloadGameScene();
    }

    /// <summary>
    /// Returns the player to the main menu scene.
    /// </summary>
    public void BackToMenu()
    {
        // Ensure time is running before changing scenes
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>
    /// Utility to ensure time scale is reset and current scene is reloaded.
    /// </summary>
    private void ReloadGameScene()
    {
        Time.timeScale = 1f;
        // Loads the active scene name to ensure we stay in the gameplay loop
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
