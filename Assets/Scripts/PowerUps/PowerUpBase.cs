using UnityEngine;

/// <summary>
/// Basis-Klasse für alle PowerUps (ScriptableObject).
/// Erstelle neue PowerUps über: Create → PowerUps → [PowerUpName]
/// </summary>
public abstract class PowerUpBase : ScriptableObject
{
    [Header("Info")]
    public string powerUpName = "PowerUp";
    [TextArea(2, 4)]
    public string description = "Beschreibung des PowerUps";
    public Sprite icon;
    
    [Header("Stacking")]
    [Tooltip("Kann dieses PowerUp mehrfach gewählt werden?")]
    public bool canStack = true;
    
    [Header("Value Range")]
    [Tooltip("Minimaler Wert (z.B. +5% Speed)")]
    public float minValue = 5f;
    [Tooltip("Maximaler Wert (z.B. +15% Speed)")]
    public float maxValue = 15f;
    
    /// <summary>
    /// Wendet das PowerUp auf den Spieler an.
    /// Gibt den tatsächlich angewendeten Wert zurück (für Tracking).
    /// </summary>
    public abstract float Apply(PlayerManager player);
    
    /// <summary>
    /// Entfernt das PowerUp vom Spieler (für Reset).
    /// </summary>
    public abstract void Remove(PlayerManager player, float appliedValue);
    
    /// <summary>
    /// Generiert einen zufälligen Wert zwischen min und max.
    /// </summary>
    protected float GetRandomValue()
    {
        return Random.Range(minValue, maxValue);
    }
    
    /// <summary>
    /// Gibt die Beschreibung mit dem aktuellen Wert zurück.
    /// Überschreibe dies für spezifische Formatierung.
    /// </summary>
    public virtual string GetDescription(float value)
    {
        return description.Replace("{value}", value.ToString("F1"));
    }
}
