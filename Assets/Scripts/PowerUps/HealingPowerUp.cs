using UnityEngine;

/// <summary>
/// Heilt Herzen und/oder gibt zusätzliche Max-Herzen.
/// </summary>
[CreateAssetMenu(fileName = "HealingPowerUp", menuName = "PowerUps/Healing PowerUp")]
public class HealingPowerUp : PowerUpBase
{
    public enum HealingMode
    {
        HealOnly,           // Nur heilen (kein permanenter Buff)
        MaxHeartOnly,       // Nur +1 Max Herz
        HealAndMaxHeart     // Beides
    }
    
    [Header("Healing Settings")]
    public HealingMode mode = HealingMode.HealAndMaxHeart;
    // Heilmenge: minValue bis maxValue aus PowerUpBase
    
    [Header("Max Hearts")]
    [Tooltip("Anzahl zusätzlicher Max-Herzen")]
    public int maxHeartBonus = 1;
    
    public override float Apply(PlayerManager player)
    {
        int healAmount = Mathf.RoundToInt(GetRandomValue());
        float appliedMaxHearts = 0f;
        
        // Max Herzen erhöhen
        if (mode == HealingMode.MaxHeartOnly || mode == HealingMode.HealAndMaxHeart)
        {
            player.AddMaxHeart(maxHeartBonus);
            appliedMaxHearts = maxHeartBonus;
        }
        
        // Heilen
        if (mode == HealingMode.HealOnly || mode == HealingMode.HealAndMaxHeart)
        {
            player.HealHearts(healAmount);
        }
        
        // Nur Max Hearts tracken (Heilung ist temporär)
        return appliedMaxHearts;
    }
    
    public override void Remove(PlayerManager player, float appliedValue)
    {
        // Max Hearts reduzieren
        player.maxHearts -= (int)appliedValue;
        player.maxHearts = Mathf.Max(player.maxHearts, 1); // Mindestens 1 Herz
        
        // Current Hearts anpassen falls über Max
        if (player.CurrentHearts > player.maxHearts)
        {
            // Clamp durch HealHearts mit 0
            player.HealHearts(0);
        }
    }
    
    public override string GetDescription(float value)
    {
        int healAmount = Mathf.RoundToInt(value);
        
        switch (mode)
        {
            case HealingMode.HealOnly:
                return healAmount == 1 
                    ? "Heilt 1 Herz"
                    : $"Heilt {healAmount} Herzen";
                    
            case HealingMode.MaxHeartOnly:
                return maxHeartBonus == 1
                    ? "+1 Max Herz"
                    : $"+{maxHeartBonus} Max Herzen";
                
            case HealingMode.HealAndMaxHeart:
                string healStr = healAmount == 1 ? "1 Herz" : $"{healAmount} Herzen";
                string maxStr = maxHeartBonus == 1 ? "+1 Max Herz" : $"+{maxHeartBonus} Max Herzen";
                return $"Heilt {healStr}\n{maxStr}";
                
            default:
                return description;
        }
    }
    
    void Reset()
    {
        powerUpName = "Healing";
        description = "Heilt dich und gibt ein zusätzliches Herz";
        minValue = 1f;  // 1 Herz heilen
        maxValue = 2f;  // 2 Herzen heilen
        maxHeartBonus = 1;
        canStack = true;
        mode = HealingMode.HealAndMaxHeart;
    }
}
