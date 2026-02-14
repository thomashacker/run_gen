using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager Instance { get; private set; }
    
    // Game States
    public enum GameState { Playing, GameOver }
    public GameState CurrentState { get; private set; } = GameState.Playing;
    
    // Events
    public event Action OnGameOver;
    public event Action OnGameRestart;
    
    // Distance (driven by world scroll, not player position)
    public float Distance { get; private set; } = 0f;
    public float HighScore { get; private set; } = 0f;
    
    /// <summary>Current world scroll speed in units/second. 0 if no AutoScrollController.</summary>
    public float CurrentSpeed
    {
        get
        {
            var scroll = World2.AutoScrollController.Instance;
            return scroll != null ? scroll.CurrentSpeed : 0f;
        }
    }
    
    [Header("References")]
    public Transform player;
    
    void Awake()
    {
        // Singleton Setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Load HighScore
        HighScore = PlayerPrefs.GetFloat("HighScore", 0f);
    }
    
    void Start()
    {
        // Spieler automatisch finden falls nicht zugewiesen
        if (player == null)
        {
            PlayerManager pm = FindAnyObjectByType<PlayerManager>();
            if (pm != null)
            {
                player = pm.transform;
            }
        }
    }
    
    void Update()
    {
        // Distance = total world scroll distance
        if (CurrentState == GameState.Playing)
        {
            var scroll = World2.AutoScrollController.Instance;
            if (scroll != null)
                Distance = scroll.TotalScrolled;
        }
    }
    
    /// <summary>
    /// Wird aufgerufen wenn der Spieler stirbt
    /// </summary>
    public void PlayerDied()
    {
        if (CurrentState == GameState.GameOver) return; // Bereits tot
        
        CurrentState = GameState.GameOver;
        
        // HighScore speichern
        if (Distance > HighScore)
        {
            HighScore = Distance;
            PlayerPrefs.SetFloat("HighScore", HighScore);
            PlayerPrefs.Save();
        }
        
        Debug.Log($"Game Over! Distance: {Distance:F1}m | HighScore: {HighScore:F1}m");
        
        // LevelManager Reset (Rogue-like)
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ResetProgress();
        }
        
        // Event auslösen
        OnGameOver?.Invoke();
    }
    
    /// <summary>
    /// Startet das Spiel neu
    /// </summary>
    public void RestartGame()
    {
        // Event vor Scene-Reload auslösen
        OnGameRestart?.Invoke();
        
        // Scene neu laden
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    [Header("Scene Settings")]
    public string mainMenuSceneName = "Start";
    
    /// <summary>
    /// Geht zurück zum Hauptmenü
    /// </summary>
    public void ExitGame()
    {
        Debug.Log("Returning to Main Menu...");
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    /// <summary>
    /// Prüft ob das Spiel gerade läuft
    /// </summary>
    public bool IsPlaying()
    {
        return CurrentState == GameState.Playing;
    }
    
    /// <summary>
    /// Gibt den formatierten Distance-String zurück
    /// </summary>
    public string GetDistanceText()
    {
        return $"{Distance:F0}m";
    }
    
    /// <summary>
    /// Gibt den formatierten HighScore-String zurück
    /// </summary>
    public string GetHighScoreText()
    {
        return $"{HighScore:F0}m";
    }
    
}
