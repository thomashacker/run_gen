using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Verwaltet XP, Level und PowerUps.
/// Rogue-like: Alles wird bei Tod/Neustart zurückgesetzt.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    
    [Header("Level Settings")]
    [Tooltip("XP benötigt für Level 1 (verdoppelt sich pro Level)")]
    public int baseXpRequired = 5;
    
    [Header("PowerUps")]
    [Tooltip("Alle verfügbaren PowerUps (ScriptableObjects)")]
    public List<PowerUpBase> availablePowerUps = new List<PowerUpBase>();
    [Tooltip("Anzahl PowerUps die bei Level-Up angeboten werden")]
    public int powerUpChoices = 3;
    
    [Header("References")]
    public PlayerManager player;
    
    // Events
    public event Action<int> OnXpGained;              // (amount)
    public event Action<int, int> OnXpChanged;        // (currentXp, xpToNext)
    public event Action<int> OnLevelUp;               // (newLevel)
    public event Action<List<PowerUpBase>> OnShowLevelUpPanel; // (choices)
    public event Action OnLevelUpComplete;
    
    // State
    public int CurrentLevel { get; private set; } = 0;
    public int CurrentXp { get; private set; } = 0;
    public int XpToNextLevel => CalculateXpForLevel(CurrentLevel + 1);
    public float XpProgress => XpToNextLevel > 0 ? (float)CurrentXp / XpToNextLevel : 0f;
    
    // Tracking der angewendeten PowerUps (für UI/Debug)
    private List<AppliedPowerUp> appliedPowerUps = new List<AppliedPowerUp>();
    
    [System.Serializable]
    public class AppliedPowerUp
    {
        public PowerUpBase powerUp;
        public float appliedValue;
        public int stackCount;
    }
    
    void Awake()
    {
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
        // Player finden
        if (player == null)
            player = FindAnyObjectByType<PlayerManager>();
        
        // Initial State
        ResetProgress();
    }
    
    /// <summary>
    /// Berechnet die benötigte XP für ein bestimmtes Level.
    /// Level 1 = baseXp, Level 2 = baseXp * 2, Level 3 = baseXp * 4, etc.
    /// </summary>
    public int CalculateXpForLevel(int level)
    {
        if (level <= 0) return 0;
        return baseXpRequired * (1 << (level - 1)); // 2^(level-1) * baseXp
    }
    
    /// <summary>
    /// Fügt XP hinzu. Löst Level-Up aus wenn genug XP.
    /// </summary>
    public void AddXp(int amount)
    {
        if (amount <= 0) return;
        
        CurrentXp += amount;
        OnXpGained?.Invoke(amount);
        OnXpChanged?.Invoke(CurrentXp, XpToNextLevel);
        
        Debug.Log($"[LevelManager] +{amount} XP. Total: {CurrentXp}/{XpToNextLevel}");
        
        // Level-Up Check
        CheckLevelUp();
    }
    
    void CheckLevelUp()
    {
        while (CurrentXp >= XpToNextLevel)
        {
            // XP abziehen und Level erhöhen
            CurrentXp -= XpToNextLevel;
            CurrentLevel++;
            
            Debug.Log($"[LevelManager] LEVEL UP! Now Level {CurrentLevel}");
            
            // Event und Panel
            OnLevelUp?.Invoke(CurrentLevel);
            ShowLevelUpPanel();
            
            // XP Changed Event nach Level-Up
            OnXpChanged?.Invoke(CurrentXp, XpToNextLevel);
            
            // Nur ein Level-Up pro Frame (Panel muss erst geschlossen werden)
            break;
        }
    }
    
    void ShowLevelUpPanel()
    {
        // Spiel pausieren
        Time.timeScale = 0f;
        
        // Zufällige PowerUps auswählen
        List<PowerUpBase> choices = GetRandomPowerUps(powerUpChoices);
        
        // Event für UI
        OnShowLevelUpPanel?.Invoke(choices);
    }
    
    /// <summary>
    /// Wählt zufällige PowerUps aus der verfügbaren Liste.
    /// </summary>
    List<PowerUpBase> GetRandomPowerUps(int count)
    {
        List<PowerUpBase> result = new List<PowerUpBase>();
        List<PowerUpBase> available = new List<PowerUpBase>(availablePowerUps);
        
        // Shuffle und nehme die ersten 'count'
        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, available.Count);
            result.Add(available[randomIndex]);
            
            // Entfernen wenn nicht stackbar, sonst kann es nochmal kommen
            if (!available[randomIndex].canStack)
            {
                available.RemoveAt(randomIndex);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Wird vom UI aufgerufen wenn der Spieler ein PowerUp wählt.
    /// </summary>
    public void SelectPowerUp(PowerUpBase powerUp)
    {
        if (powerUp == null || player == null) return;
        
        // PowerUp anwenden
        float appliedValue = powerUp.Apply(player);
        
        // Tracking
        TrackPowerUp(powerUp, appliedValue);
        
        Debug.Log($"[LevelManager] Applied {powerUp.powerUpName}: {appliedValue:F1}");
        
        // Spiel fortsetzen
        Time.timeScale = 1f;
        
        OnLevelUpComplete?.Invoke();
        
        // Check ob weiterer Level-Up pending ist
        CheckLevelUp();
    }
    
    void TrackPowerUp(PowerUpBase powerUp, float value)
    {
        // Existierendes Tracking suchen
        var existing = appliedPowerUps.Find(a => a.powerUp == powerUp);
        
        if (existing != null)
        {
            existing.appliedValue += value;
            existing.stackCount++;
        }
        else
        {
            appliedPowerUps.Add(new AppliedPowerUp
            {
                powerUp = powerUp,
                appliedValue = value,
                stackCount = 1
            });
        }
    }
    
    /// <summary>
    /// Setzt Level, XP und alle PowerUps zurück (Rogue-like).
    /// </summary>
    public void ResetProgress()
    {
        // PowerUps rückgängig machen
        if (player != null)
        {
            foreach (var applied in appliedPowerUps)
            {
                if (applied.powerUp != null)
                {
                    applied.powerUp.Remove(player, applied.appliedValue);
                }
            }
        }
        
        appliedPowerUps.Clear();
        CurrentLevel = 0;
        CurrentXp = 0;
        
        OnXpChanged?.Invoke(CurrentXp, XpToNextLevel);
        
        Debug.Log("[LevelManager] Progress reset");
    }
    
    /// <summary>
    /// Gibt die Liste der angewendeten PowerUps zurück (für UI).
    /// </summary>
    public List<AppliedPowerUp> GetAppliedPowerUps()
    {
        return new List<AppliedPowerUp>(appliedPowerUps);
    }
}
