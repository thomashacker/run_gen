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
    
    // Score (Distanz-basiert)
    public float Score { get; private set; } = 0f;
    public float HighScore { get; private set; } = 0f;
    public float DistanceInMeters => Score;
    
    [Header("References")]
    public Transform player;
    
    private float playerStartX;
    
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
        
        // Startposition merken
        if (player != null)
        {
            playerStartX = player.position.x;
        }
    }
    
    void Update()
    {
        // Score nur während des Spielens berechnen (Distanz-basiert)
        if (CurrentState == GameState.Playing && player != null)
        {
            // Distanz = aktuelle X-Position - Startposition
            float distance = player.position.x - playerStartX;
            Score = Mathf.Max(Score, distance); // Nur vorwärts zählen
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
        if (Score > HighScore)
        {
            HighScore = Score;
            PlayerPrefs.SetFloat("HighScore", HighScore);
            PlayerPrefs.Save();
        }
        
        Debug.Log($"Game Over! Distance: {Score:F1}m | HighScore: {HighScore:F1}m");
        
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
    
    /// <summary>
    /// Beendet das Spiel (oder geht zum Hauptmenü)
    /// </summary>
    public void ExitGame()
    {
        // Später: Zurück zum Hauptmenü
        Debug.Log("Exit pressed - implement main menu later");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    /// <summary>
    /// Prüft ob das Spiel gerade läuft
    /// </summary>
    public bool IsPlaying()
    {
        return CurrentState == GameState.Playing;
    }
    
    /// <summary>
    /// Gibt den formatierten Score-String zurück
    /// </summary>
    public string GetScoreText()
    {
        return $"{Score:F0}m";
    }
    
    /// <summary>
    /// Gibt den formatierten HighScore-String zurück
    /// </summary>
    public string GetHighScoreText()
    {
        return $"{HighScore:F0}m";
    }
}
