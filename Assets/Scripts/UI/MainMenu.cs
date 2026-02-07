using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hauptmenü Controller für die Start Scene.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Name der Game Scene")]
    public string gameSceneName = "Game";
    
    /// <summary>
    /// Startet das Spiel (lädt Game Scene)
    /// </summary>
    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }
    
    /// <summary>
    /// Beendet die Application
    /// </summary>
    public void ExitApplication()
    {
        Debug.Log("Exiting Application...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
