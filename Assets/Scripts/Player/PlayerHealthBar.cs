using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP-Bar die über dem Spieler schwebt.
/// Nutzt World Space Canvas für die Anzeige.
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Der PlayerManager (wird automatisch gesucht wenn leer)")]
    public PlayerManager player;
    
    [Header("UI Elements")]
    [Tooltip("Das Image das als Füllung dient (Image Type = Filled)")]
    public Image fillImage;
    [Tooltip("Hintergrund der Health Bar (optional)")]
    public Image backgroundImage;
    
    [Header("Colors")]
    public Color healthyColor = new Color(0.2f, 0.8f, 0.2f); // Grün
    public Color warnColor = new Color(1f, 0.8f, 0f);        // Gelb
    public Color dangerColor = new Color(0.9f, 0.2f, 0.2f);  // Rot
    [Tooltip("HP-Prozent unter dem die Warn-Farbe verwendet wird")]
    [Range(0f, 1f)] public float warnThreshold = 0.5f;
    [Tooltip("HP-Prozent unter dem die Danger-Farbe verwendet wird")]
    [Range(0f, 1f)] public float dangerThreshold = 0.25f;
    
    [Header("Position")]
    [Tooltip("Offset über dem Spieler")]
    public Vector3 offset = new Vector3(0, 1.5f, 0);
    
    [Header("Animation")]
    [Tooltip("Geschwindigkeit der Füll-Animation")]
    public float fillSpeed = 5f;
    [Tooltip("Dauer für die der Balken bei Schaden blinkt")]
    public float flashDuration = 0.1f;
    public Color flashColor = Color.white;
    
    [Header("Visibility")]
    [Tooltip("Versteckt die Bar wenn HP voll ist")]
    public bool hideWhenFull = true;
    [Tooltip("Zeigt die Bar für X Sekunden nach Schaden/Heilung")]
    public float showDuration = 3f;
    
    private float targetFillAmount = 1f;
    private float currentFillAmount = 1f;
    private float showTimer = 0f;
    private float flashTimer = 0f;
    private Color originalFillColor;
    private CanvasGroup canvasGroup;
    
    void Awake()
    {
        // Player finden
        if (player == null)
            player = GetComponentInParent<PlayerManager>();
        
        if (player == null)
            player = FindAnyObjectByType<PlayerManager>();
        
        // CanvasGroup für Visibility
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        if (fillImage != null)
            originalFillColor = fillImage.color;
    }
    
    void Start()
    {
        if (player != null)
        {
            // Events abonnieren
            player.OnHealthChanged += OnHealthChanged;
            player.OnDamaged += OnDamaged;
            
            // Initial Update
            UpdateHealthBar(player.CurrentHealth, player.maxHealth, false);
        }
        
        // Initial verstecken wenn gewünscht
        if (hideWhenFull)
            SetVisibility(false);
    }
    
    void OnDestroy()
    {
        if (player != null)
        {
            player.OnHealthChanged -= OnHealthChanged;
            player.OnDamaged -= OnDamaged;
        }
    }
    
    void Update()
    {
        // Position über Spieler halten
        if (player != null)
        {
            transform.position = player.transform.position + offset;
        }
        
        // Smooth Fill Animation
        if (fillImage != null && Mathf.Abs(currentFillAmount - targetFillAmount) > 0.001f)
        {
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, Time.deltaTime * fillSpeed);
            fillImage.fillAmount = currentFillAmount;
        }
        
        // Flash Animation
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && fillImage != null)
            {
                fillImage.color = GetHealthColor(targetFillAmount);
            }
        }
        
        // Visibility Timer
        if (hideWhenFull && showTimer > 0f)
        {
            showTimer -= Time.deltaTime;
            if (showTimer <= 0f && targetFillAmount >= 1f)
            {
                SetVisibility(false);
            }
        }
    }
    
    void OnHealthChanged(int currentHealth, int maxHealth)
    {
        UpdateHealthBar(currentHealth, maxHealth, true);
    }
    
    void OnDamaged()
    {
        // Flash-Effekt
        if (fillImage != null)
        {
            fillImage.color = flashColor;
            flashTimer = flashDuration;
        }
    }
    
    void UpdateHealthBar(int currentHealth, int maxHealth, bool animate)
    {
        float percent = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        targetFillAmount = percent;
        
        if (!animate)
        {
            currentFillAmount = percent;
            if (fillImage != null)
                fillImage.fillAmount = percent;
        }
        
        // Farbe aktualisieren (außer während Flash)
        if (flashTimer <= 0f && fillImage != null)
        {
            fillImage.color = GetHealthColor(percent);
        }
        
        // Sichtbarkeit
        if (hideWhenFull)
        {
            if (percent < 1f)
            {
                SetVisibility(true);
                showTimer = showDuration;
            }
            else
            {
                showTimer = showDuration; // Timer starten um sanft auszublenden
            }
        }
    }
    
    Color GetHealthColor(float percent)
    {
        if (percent <= dangerThreshold)
            return dangerColor;
        if (percent <= warnThreshold)
            return warnColor;
        return healthyColor;
    }
    
    void SetVisibility(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
    
    /// <summary>
    /// Erzwingt die Anzeige der Health Bar für eine bestimmte Zeit.
    /// </summary>
    public void ForceShow(float duration = -1f)
    {
        SetVisibility(true);
        showTimer = duration > 0 ? duration : showDuration;
    }
}
