using UnityEngine;

/// <summary>
/// Erhöht die Sprungkraft des Spielers.
/// </summary>
[CreateAssetMenu(fileName = "JumpForcePowerUp", menuName = "PowerUps/Jump Force PowerUp")]
public class JumpForcePowerUp : PowerUpBase
{
    [Header("Jump Settings")]
    [Tooltip("Wenn true, ist der Wert ein Prozentsatz der aktuellen JumpForce")]
    public bool isPercentage = true;
    
    public override float Apply(PlayerManager player)
    {
        float value = GetRandomValue();
        
        if (isPercentage)
        {
            // Prozentuale Erhöhung
            float increase = player.jumpForce * (value / 100f);
            player.jumpForce += increase;
            return increase;
        }
        else
        {
            // Absoluter Wert
            player.jumpForce += value;
            return value;
        }
    }
    
    public override void Remove(PlayerManager player, float appliedValue)
    {
        player.jumpForce -= appliedValue;
    }
    
    public override string GetDescription(float value)
    {
        if (isPercentage)
            return $"Sprungkraft +{value:F0}%";
        else
            return $"Sprungkraft +{value:F1}";
    }
    
    void Reset()
    {
        powerUpName = "Super Jump";
        description = "Erhöht deine Sprungkraft um {value}%";
        minValue = 5f;
        maxValue = 15f;
        canStack = true;
        isPercentage = true;
    }
}
