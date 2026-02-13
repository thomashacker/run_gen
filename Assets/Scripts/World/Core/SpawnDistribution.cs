using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Modus für die Gewichtsberechnung basierend auf Distanz.
    /// </summary>
    public enum DistributionMode
    {
        /// <summary>Gauß-Kurve (Glockenkurve) mit Peak, Breite und Minimum.</summary>
        Gaussian,
        /// <summary>Freie AnimationCurve für volle Kontrolle über die Gewichtskurve.</summary>
        CustomCurve
    }

    /// <summary>
    /// Ein spawbares Objekt mit distanzbasierter Gewichtung.
    /// Wiederverwendbar für Emeralds, Enemies, Items, Power-Ups, etc.
    /// 
    /// Gaussian-Modus: Glockenkurve mit konfigurierbarem Peak, Breite und Minimum.
    /// CustomCurve-Modus: Freie AnimationCurve für beliebige Formen.
    /// 
    /// Das minWeight sorgt dafür, dass ein Item nie komplett verschwindet —
    /// der Spieler hat immer eine kleine Chance, seltene Items zu finden.
    /// </summary>
    [System.Serializable]
    public class SpawnableEntry
    {
        public string name = "Item";
        public GameObject prefab;
        
        [Header("Distribution Mode")]
        [Tooltip("Gaussian = Glockenkurve mit Peak/Spread. CustomCurve = freie AnimationCurve.")]
        public DistributionMode mode = DistributionMode.Gaussian;
        
        [Header("Gaussian Settings")]
        [Tooltip("Distanz (in Tiles) wo dieses Item die höchste Spawn-Chance hat.")]
        public float peakDistance = 250f;
        
        [Tooltip("Breite der Glockenkurve (Standardabweichung in Tiles). Größer = breitere Verteilung.")]
        public float spread = 200f;
        
        [Tooltip("Maximales Gewicht am Peak der Kurve.")]
        public float maxWeight = 1f;
        
        [Tooltip("Minimales Gewicht — Chance sinkt nie unter diesen Wert. Verhindert dass Items komplett verschwinden.")]
        [Range(0f, 1f)]
        public float minWeight = 0.05f;
        
        [Header("Custom Curve Settings")]
        [Tooltip("X-Achse = Distanz (in Tiles), Y-Achse = Gewicht. Wird nur benutzt wenn Mode = CustomCurve.")]
        public AnimationCurve customCurve = AnimationCurve.EaseInOut(0f, 0f, 1000f, 1f);
        
        /// <summary>
        /// Berechnet das Spawn-Gewicht für eine gegebene Distanz.
        /// </summary>
        /// <param name="distance">Aktuelle Distanz in Tiles (worldX).</param>
        /// <returns>Gewicht >= minWeight.</returns>
        public float EvaluateWeight(float distance)
        {
            switch (mode)
            {
                case DistributionMode.Gaussian:
                    return EvaluateGaussian(distance);
                case DistributionMode.CustomCurve:
                    return EvaluateCustomCurve(distance);
                default:
                    return minWeight;
            }
        }
        
        /// <summary>
        /// Gauß-Formel: minWeight + (maxWeight - minWeight) * e^(-0.5 * ((x - peak) / spread)^2)
        /// Erzeugt eine Glockenkurve die am Peak maxWeight erreicht und nie unter minWeight fällt.
        /// </summary>
        float EvaluateGaussian(float distance)
        {
            float safeSigma = Mathf.Max(spread, 0.001f);
            float exponent = -0.5f * Mathf.Pow((distance - peakDistance) / safeSigma, 2);
            float gaussian = Mathf.Exp(exponent);
            return minWeight + (maxWeight - minWeight) * gaussian;
        }
        
        /// <summary>
        /// Wertet die benutzerdefinierte AnimationCurve aus, mit minWeight als Untergrenze.
        /// </summary>
        float EvaluateCustomCurve(float distance)
        {
            float value = customCurve.Evaluate(distance);
            return Mathf.Max(value, minWeight);
        }
    }
    
    /// <summary>
    /// Statische Utility-Klasse für distanzbasierte Spawn-Auswahl.
    /// Kann von jedem Pass verwendet werden (Emeralds, Enemies, Items, etc.).
    /// 
    /// Verwendung:
    ///   SpawnableEntry selected = SpawnDistribution.SelectWeighted(entries, worldX);
    ///   if (selected != null) Instantiate(selected.prefab, ...);
    /// </summary>
    public static class SpawnDistribution
    {
        /// <summary>
        /// Wählt einen Eintrag basierend auf distanzgewichteter Wahrscheinlichkeit.
        /// Jeder Eintrag wird an der aktuellen Distanz evaluiert, dann gewichtet ausgewählt.
        /// </summary>
        /// <param name="entries">Liste der spawbaren Einträge mit Distanzkurven.</param>
        /// <param name="distance">Aktuelle Distanz in Tiles (worldX).</param>
        /// <returns>Der ausgewählte Eintrag, oder null wenn die Liste leer ist.</returns>
        public static SpawnableEntry SelectWeighted(List<SpawnableEntry> entries, float distance)
        {
            if (entries == null || entries.Count == 0) return null;
            if (entries.Count == 1) return entries[0];
            
            // Gewichte an aktueller Distanz berechnen
            float totalWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                totalWeight += entries[i].EvaluateWeight(distance);
            }
            
            if (totalWeight <= 0f) return entries[0];
            
            // Gewichtete Zufallsauswahl
            float random = Random.value * totalWeight;
            float cumulative = 0f;
            
            for (int i = 0; i < entries.Count; i++)
            {
                cumulative += entries[i].EvaluateWeight(distance);
                if (random <= cumulative)
                    return entries[i];
            }
            
            return entries[entries.Count - 1];
        }
        
        /// <summary>
        /// Gibt normalisierte Gewichte (0-1, Summe = 1) für alle Einträge bei gegebener Distanz zurück.
        /// Nützlich für Debug-Anzeige und Visualisierung.
        /// </summary>
        public static float[] GetNormalizedWeights(List<SpawnableEntry> entries, float distance)
        {
            if (entries == null || entries.Count == 0) return new float[0];
            
            float[] weights = new float[entries.Count];
            float total = 0f;
            
            for (int i = 0; i < entries.Count; i++)
            {
                weights[i] = entries[i].EvaluateWeight(distance);
                total += weights[i];
            }
            
            if (total > 0f)
            {
                for (int i = 0; i < weights.Length; i++)
                    weights[i] /= total;
            }
            
            return weights;
        }
        
        /// <summary>
        /// Gibt einen Debug-String mit allen Gewichten bei gegebener Distanz zurück.
        /// Format: "[Distance=500] Green Emerald: 30.2%, Blue Emerald: 69.8%"
        /// </summary>
        public static string GetDebugString(List<SpawnableEntry> entries, float distance)
        {
            if (entries == null || entries.Count == 0) return "No entries";
            
            float[] normalized = GetNormalizedWeights(entries, distance);
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Distance={distance:F0}] ");
            
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{entries[i].name}: {normalized[i]:P1}");
            }
            
            return sb.ToString();
        }
    }
}
