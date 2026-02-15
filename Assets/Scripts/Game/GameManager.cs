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
    
    /// <summary>
    /// Current speed in units/second.
    /// Auto-scroll mode: world scroll speed.
    /// Player-driven mode: player's horizontal speed (from Rigidbody2D).
    /// </summary>
    public float CurrentSpeed
    {
        get
        {
            var scroll = World2.AutoScrollController.Instance;
            if (scroll == null) return 0f;

            if (scroll.autoScrollEnabled)
                return scroll.CurrentSpeed;

            // Player-driven: show the player's actual horizontal speed
            return playerRb != null ? Mathf.Abs(playerRb.linearVelocity.x) : 0f;
        }
    }
    
    [Header("References")]
    public Transform player;
    
    [Tooltip("Optional: pursuing kill zone (player-driven mode). Used to track distance between player and the wall.")]
    public World2.ScrollKillZone killZone;
    
    /// <summary>
    /// Distance in units between the player and the pursuing kill zone.
    /// -1 if no kill zone is assigned or it's in Static mode.
    /// </summary>
    public float KillZoneDistance { get; private set; } = -1f;
    
    private Rigidbody2D playerRb;
    
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
        
        // Cache Rigidbody2D for player-driven speed readout
        if (player != null)
            playerRb = player.GetComponent<Rigidbody2D>();
    }
    
    void Update()
    {
        if (CurrentState != GameState.Playing) return;
        
        // Distance = total progression
        var scroll = World2.AutoScrollController.Instance;
        if (scroll != null)
            Distance = scroll.TotalScrolled;
        
        // Kill zone distance (player-driven mode)
        if (killZone != null && player != null &&
            killZone.mode == World2.ScrollKillZone.KillZoneMode.Pursuing)
        {
            // Horizontal distance between player and the right edge of the kill zone
            float zoneRightEdge = killZone.transform.position.x + killZone.zoneWidth * 0.5f;
            KillZoneDistance = Mathf.Max(0f, player.position.x - zoneRightEdge);
        }
        else
        {
            KillZoneDistance = -1f;
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
