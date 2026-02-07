using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject gameOverPanel;         // Das Panel das angezeigt wird
    public TextMeshProUGUI scoreText;        // Score Anzeige
    public TextMeshProUGUI highScoreText;    // HighScore Anzeige
    public Button restartButton;
    public Button exitButton;
    
    [Header("Score Display (während Spiel)")]
    public TextMeshProUGUI liveScoreText;    // Optional: Live Score während des Spiels
    
    void Start()
    {
        // Alles verstecken beim Start
        HideGameOverUI();
        
        // Button Listener hinzufügen
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitClicked);
        }
        
        // Auf GameOver Event hören
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += ShowGameOverScreen;
        }
    }
    
    /// <summary>
    /// Versteckt alle Game Over UI Elemente
    /// </summary>
    void HideGameOverUI()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
            
        if (scoreText != null)
            scoreText.gameObject.SetActive(false);
            
        if (highScoreText != null)
            highScoreText.gameObject.SetActive(false);
            
        if (restartButton != null)
            restartButton.gameObject.SetActive(false);
            
        if (exitButton != null)
            exitButton.gameObject.SetActive(false);
    }
    
    void Update()
    {
        // Live Score updaten
        if (liveScoreText != null && GameManager.Instance != null && GameManager.Instance.IsPlaying())
        {
            liveScoreText.text = $"Score: {GameManager.Instance.Score:F0}";
        }
    }
    
    void OnDestroy()
    {
        // Event Listener entfernen
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= ShowGameOverScreen;
        }
    }
    
    /// <summary>
    /// Zeigt den Game Over Screen
    /// </summary>
    void ShowGameOverScreen()
    {
        // Panel anzeigen
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        
        // Score anzeigen
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            if (GameManager.Instance != null)
                scoreText.text = $"Score: {GameManager.Instance.Score:F0}";
        }
        
        // HighScore anzeigen
        if (highScoreText != null)
        {
            highScoreText.gameObject.SetActive(true);
            if (GameManager.Instance != null)
                highScoreText.text = $"Best: {GameManager.Instance.HighScore:F0}";
        }
        
        // Buttons anzeigen
        if (restartButton != null)
            restartButton.gameObject.SetActive(true);
            
        if (exitButton != null)
            exitButton.gameObject.SetActive(true);
        
        // Live Score verstecken
        if (liveScoreText != null)
            liveScoreText.gameObject.SetActive(false);
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
}
