using UnityEngine;

/// <summary>
/// Heilt den Spieler und/oder erhöht die maximalen HP.
/// </summary>
[CreateAssetMenu(fileName = "HealingPowerUp", menuName = "PowerUps/Healing PowerUp")]
public class HealingPowerUp : PowerUpBase
{
    public enum HealingMode
    {
        HealOnly,       // Nur heilen (kein permanenter Buff)
        MaxHpOnly,      // Nur Max HP erhöhen
        HealAndMaxHp    // Beides
    }
    
    [Header("Healing Settings")]
    public HealingMode mode = HealingMode.HealAndMaxHp;
    
    [Tooltip("Heilung als Prozent der Max HP (true) oder absoluter Wert (false)")]
    public bool healIsPercentage = true;
    
    [Header("Max HP Bonus (wenn aktiviert)")]
    [Tooltip("Max HP Erhöhung (absoluter Wert)")]
    public float maxHpBonus = 10f;
    
    public override float Apply(PlayerManager player)
    {
        float healValue = GetRandomValue();
        float appliedMaxHp = 0f;
        
        // Max HP erhöhen
        if (mode == HealingMode.MaxHpOnly || mode == HealingMode.HealAndMaxHp)
        {
            player.maxHealth += (int)maxHpBonus;
            appliedMaxHp = maxHpBonus;
        }
        
        // Heilen
        if (mode == HealingMode.HealOnly || mode == HealingMode.HealAndMaxHp)
        {
            int healAmount;
            if (healIsPercentage)
            {
                healAmount = Mathf.RoundToInt(player.maxHealth * (healValue / 100f));
            }
            else
            {
                healAmount = (int)healValue;
            }
            
            player.Heal(healAmount);
        }
        
        // Nur Max HP Änderung tracken (Heilung ist temporär)
        return appliedMaxHp;
    }
    
    public override void Remove(PlayerManager player, float appliedValue)
    {
        // Nur Max HP wird zurückgesetzt
        player.maxHealth -= (int)appliedValue;
        
        // Current HP anpassen falls über Max
        if (player.CurrentHealth > player.maxHealth)
        {
            // Direkt setzen via Reflection oder public Methode...
            // Für jetzt: Heal auf 0 um CurrentHealth zu clampen
            player.Heal(0);
        }
    }
    
    public override string GetDescription(float value)
    {
        switch (mode)
        {
            case HealingMode.HealOnly:
                return healIsPercentage 
                    ? $"Heilt {value:F0}% deiner HP"
                    : $"Heilt {value:F0} HP";
                    
            case HealingMode.MaxHpOnly:
                return $"Max HP +{maxHpBonus:F0}";
                
            case HealingMode.HealAndMaxHp:
                string healStr = healIsPercentage ? $"{value:F0}%" : $"{value:F0}";
                return $"Heilt {healStr} HP\nMax HP +{maxHpBonus:F0}";
                
            default:
                return description;
        }
    }
    
    void Reset()
    {
        powerUpName = "Healing";
        description = "Heilt dich und erhöht deine maximalen HP";
        minValue = 20f;  // 20% Heilung
        maxValue = 40f;  // 40% Heilung
        maxHpBonus = 10f;
        canStack = true;
        mode = HealingMode.HealAndMaxHp;
        healIsPercentage = true;
    }
}
