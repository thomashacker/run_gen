using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// UI für Level-System: XP-Bar und Level-Up Panel.
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    [Header("XP Bar (HUD)")]
    public GameObject xpBarPanel;
    public Image xpFillImage;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    
    [Header("Level Up Panel")]
    public GameObject levelUpPanel;
    public TextMeshProUGUI levelUpTitleText;
    public Transform powerUpButtonsContainer;
    public GameObject powerUpButtonPrefab;
    
    [Header("Animation")]
    public float xpFillSpeed = 5f;
    
    private float targetFillAmount = 0f;
    private List<GameObject> currentButtons = new List<GameObject>();
    
    void Start()
    {
        // Events abonnieren
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnXpChanged += OnXpChanged;
            LevelManager.Instance.OnLevelUp += OnLevelUp;
            LevelManager.Instance.OnShowLevelUpPanel += ShowLevelUpPanel;
            LevelManager.Instance.OnLevelUpComplete += HideLevelUpPanel;
            
            // Initial Update
            UpdateXpBar(LevelManager.Instance.CurrentXp, LevelManager.Instance.XpToNextLevel, false);
            UpdateLevelText(LevelManager.Instance.CurrentLevel);
        }
        
        // Panel initial verstecken
        if (levelUpPanel != null)
            levelUpPanel.SetActive(false);
    }
    
    void OnDestroy()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnXpChanged -= OnXpChanged;
            LevelManager.Instance.OnLevelUp -= OnLevelUp;
            LevelManager.Instance.OnShowLevelUpPanel -= ShowLevelUpPanel;
            LevelManager.Instance.OnLevelUpComplete -= HideLevelUpPanel;
        }
    }
    
    void Update()
    {
        // Smooth XP Bar Animation
        if (xpFillImage != null && Mathf.Abs(xpFillImage.fillAmount - targetFillAmount) > 0.001f)
        {
            xpFillImage.fillAmount = Mathf.Lerp(xpFillImage.fillAmount, targetFillAmount, Time.unscaledDeltaTime * xpFillSpeed);
        }
    }
    
    void OnXpChanged(int currentXp, int xpToNext)
    {
        UpdateXpBar(currentXp, xpToNext, true);
    }
    
    void OnLevelUp(int newLevel)
    {
        UpdateLevelText(newLevel);
    }
    
    void UpdateXpBar(int currentXp, int xpToNext, bool animate)
    {
        float progress = xpToNext > 0 ? (float)currentXp / xpToNext : 0f;
        targetFillAmount = progress;
        
        if (!animate && xpFillImage != null)
        {
            xpFillImage.fillAmount = progress;
        }
        
        if (xpText != null)
        {
            xpText.text = $"{currentXp}/{xpToNext}";
        }
    }
    
    void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Lv.{level}";
        }
    }
    
    void ShowLevelUpPanel(List<PowerUpBase> choices)
    {
        if (levelUpPanel == null) return;
        
        // Panel anzeigen
        levelUpPanel.SetActive(true);
        
        // Titel
        if (levelUpTitleText != null)
        {
            levelUpTitleText.text = $"Level {LevelManager.Instance.CurrentLevel}!";
        }
        
        // Alte Buttons entfernen
        ClearPowerUpButtons();
        
        // Neue Buttons erstellen
        foreach (var powerUp in choices)
        {
            CreatePowerUpButton(powerUp);
        }
    }
    
    void HideLevelUpPanel()
    {
        if (levelUpPanel != null)
            levelUpPanel.SetActive(false);
        
        ClearPowerUpButtons();
    }
    
    void ClearPowerUpButtons()
    {
        foreach (var button in currentButtons)
        {
            if (button != null)
                Destroy(button);
        }
        currentButtons.Clear();
    }
    
    void CreatePowerUpButton(PowerUpBase powerUp)
    {
        if (powerUpButtonPrefab == null || powerUpButtonsContainer == null) return;
        
        GameObject buttonObj = Instantiate(powerUpButtonPrefab, powerUpButtonsContainer);
        currentButtons.Add(buttonObj);
        
        // Button Setup
        PowerUpButton buttonScript = buttonObj.GetComponent<PowerUpButton>();
        if (buttonScript != null)
        {
            // Zufälligen Wert generieren für Preview
            float previewValue = Random.Range(powerUp.minValue, powerUp.maxValue);
            buttonScript.Setup(powerUp, previewValue);
        }
        else
        {
            // Fallback: Direkt Button konfigurieren
            Button btn = buttonObj.GetComponent<Button>();
            if (btn != null)
            {
                PowerUpBase capturedPowerUp = powerUp;
                btn.onClick.AddListener(() => OnPowerUpSelected(capturedPowerUp));
            }
            
            // Text setzen
            TextMeshProUGUI text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = powerUp.powerUpName;
            }
        }
    }
    
    void OnPowerUpSelected(PowerUpBase powerUp)
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SelectPowerUp(powerUp);
        }
    }
}
