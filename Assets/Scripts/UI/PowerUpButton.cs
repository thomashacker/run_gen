using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Button für PowerUp-Auswahl im Level-Up Panel.
/// </summary>
public class PowerUpButton : MonoBehaviour
{
    [Header("UI Elements")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI valueText;
    public Button button;
    
    private PowerUpBase powerUp;
    private float previewValue;
    
    public void Setup(PowerUpBase powerUp, float previewValue)
    {
        this.powerUp = powerUp;
        this.previewValue = previewValue;
        
        // Icon
        if (iconImage != null && powerUp.icon != null)
        {
            iconImage.sprite = powerUp.icon;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }
        
        // Name
        if (nameText != null)
        {
            nameText.text = powerUp.powerUpName;
        }
        
        // Beschreibung mit Wert
        if (descriptionText != null)
        {
            descriptionText.text = powerUp.GetDescription(previewValue);
        }
        
        // Wert separat (optional)
        if (valueText != null)
        {
            valueText.text = $"+{previewValue:F0}";
        }
        
        // Button Click
        if (button == null)
            button = GetComponent<Button>();
        
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }
    
    void OnClick()
    {
        if (powerUp != null && LevelManager.Instance != null)
        {
            LevelManager.Instance.SelectPowerUp(powerUp);
        }
    }
}
