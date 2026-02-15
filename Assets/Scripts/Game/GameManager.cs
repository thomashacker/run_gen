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
    
    // Distance
    public float Distance { get; private set; } = 0f;
    public float HighScore { get; private set; } = 0f;
    
    // Speed (units per second)
    public float PlayerSpeed { get; private set; } = 0f;
    
    // Distance between player and kill zone
    public float KillZoneDistance { get; private set; } = 0f;
    
    [Header("References")]
    public Transform player;
    public GameObject killZone;
    
    private float playerStartX;
    private Vector3 previousPlayerPosition;
    
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
            previousPlayerPosition = player.position;
        }
    }
    
    void Update()
    {
        // Distance während des Spielens berechnen
        if (CurrentState == GameState.Playing && player != null)
        {
            float dist = player.position.x - playerStartX;
            Distance = Mathf.Max(Distance, dist); // Nur vorwärts zählen
            
            // Speed berechnen (units per second)
            PlayerSpeed = Vector3.Distance(player.position, previousPlayerPosition) / Time.deltaTime;
            previousPlayerPosition = player.position;
            
            // Kill Zone Distance berechnen
            if (killZone != null)
            {
                KillZoneDistance = Vector3.Distance(player.position, killZone.transform.position);
            }
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
