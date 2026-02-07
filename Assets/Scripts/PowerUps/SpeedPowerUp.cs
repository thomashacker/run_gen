using UnityEngine;

/// <summary>
/// Erhöht die Bewegungsgeschwindigkeit des Spielers.
/// </summary>
[CreateAssetMenu(fileName = "SpeedPowerUp", menuName = "PowerUps/Speed PowerUp")]
public class SpeedPowerUp : PowerUpBase
{
    [Header("Speed Settings")]
    [Tooltip("Wenn true, ist der Wert ein Prozentsatz der aktuellen Speed")]
    public bool isPercentage = true;
    
    public override float Apply(PlayerManager player)
    {
        float value = GetRandomValue();
        
        if (isPercentage)
        {
            // Prozentuale Erhöhung
            float increase = player.moveSpeed * (value / 100f);
            player.moveSpeed += increase;
            return increase;
        }
        else
        {
            // Absoluter Wert
            player.moveSpeed += value;
            return value;
        }
    }
    
    public override void Remove(PlayerManager player, float appliedValue)
    {
        player.moveSpeed -= appliedValue;
    }
    
    public override string GetDescription(float value)
    {
        if (isPercentage)
            return $"Bewegungsgeschwindigkeit +{value:F0}%";
        else
            return $"Bewegungsgeschwindigkeit +{value:F1}";
    }
    
    void Reset()
    {
        powerUpName = "Speed Boost";
        description = "Erhöht deine Bewegungsgeschwindigkeit um {value}%";
        minValue = 5f;
        maxValue = 15f;
        canStack = true;
        isPercentage = true;
    }
}
