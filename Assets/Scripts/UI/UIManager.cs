using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Universeller UI Manager für das gesamte Spiel.
/// Verwaltet HUD, Game Over Screen, etc.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("HUD - Während des Spiels")]
    public GameObject hudPanel;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI killZoneDistanceText;
    public TextMeshProUGUI speedBoostText;
    
    [Header("Game Over Screen")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalDistanceText;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI finalLevelText;
    public Button restartButton;
    public Button exitButton;
    
    [Header("Settings")]
    public string distanceFormat = "{0:F0}m";
    public string speedFormat = "{0:F1} u/s";
    public string killZoneDistanceFormat = "{0:F1}m";
    public string speedBoostFormat = "Boost: {0:P0}";
    
    private PlayerManager cachedPlayer;
    
    void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // UI Initial State
        ShowHUD();
        HideGameOver();
        
        // Button Listener
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);
        
        // GameManager Events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += OnGameOver;
        }
        
        // Initial Update
        UpdateHUD();
    }
    
    void Update()
    {
        // HUD updaten während des Spiels
        if (GameManager.Instance != null && GameManager.Instance.IsPlaying())
        {
            UpdateHUD();
        }
    }
    
    void OnDestroy()
    {
        // Event Listener entfernen
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= OnGameOver;
        }
    }
    
    #region HUD
    
    void ShowHUD()
    {
        if (hudPanel != null)
            hudPanel.SetActive(true);
    }
    
    void HideHUD()
    {
        if (hudPanel != null)
            hudPanel.SetActive(false);
    }
    
    void UpdateHUD()
    {
        if (GameManager.Instance == null) return;
        
        // Distance
        if (distanceText != null)
        {
            distanceText.text = string.Format(distanceFormat, GameManager.Instance.Distance);
        }
        
        // Speed
        if (speedText != null)
        {
            speedText.text = string.Format(speedFormat, GameManager.Instance.PlayerSpeed);
        }
        
        // Kill Zone Distance
        if (killZoneDistanceText != null)
        {
            killZoneDistanceText.text = string.Format(killZoneDistanceFormat, GameManager.Instance.KillZoneDistance);
        }
        
        // Speed Boost Ratio
        if (speedBoostText != null)
        {
            if (cachedPlayer == null)
                cachedPlayer = FindAnyObjectByType<PlayerManager>();
            if (cachedPlayer != null)
                speedBoostText.text = string.Format(speedBoostFormat, cachedPlayer.SpeedBoostRatio);
        }
    }
    
    #endregion
    
    #region Game Over
    
    void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
    
    void ShowGameOver()
    {
        // HUD verstecken
        HideHUD();
        
        // Game Over Panel anzeigen
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        
        if (GameManager.Instance == null) return;
        
        // Final Distance
        if (finalDistanceText != null)
        {
            finalDistanceText.text = $"Distance: {GameManager.Instance.Distance:F0}m";
        }
        
        // High Score
        if (highScoreText != null)
        {
            highScoreText.text = $"Best: {GameManager.Instance.HighScore:F0}m";
        }
        
        // Final Level
        if (finalLevelText != null && LevelManager.Instance != null)
        {
            finalLevelText.text = $"Level: {LevelManager.Instance.CurrentLevel}";
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnGameOver()
    {
        ShowGameOver();
    }
    
    void OnRestartClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
        }
    }
    
    void OnExitClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ExitGame();
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Zeigt eine temporäre Nachricht an (für später)
    /// </summary>
    public void ShowMessage(string message, float duration = 2f)
    {
        Debug.Log($"[UI] {message}");
        // TODO: Implement toast/notification system
    }
    
    #endregion
}
