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
        /// <summary>Steigt sanft von Min auf Max und bleibt dort. Ideal für "ab Distanz X erscheinen".</summary>
        RisingPlateau,
        /// <summary>Freie AnimationCurve für volle Kontrolle über die Gewichtskurve.</summary>
        CustomCurve
    }

    /// <summary>
    /// Wiederverwendbare distanzbasierte Kurve.
    /// Kann überall eingebettet werden wo ein Wert von der Distanz abhängen soll:
    /// Spawn-Wahrscheinlichkeit, Gewichtung, Intensität, etc.
    /// 
    /// Gaussian-Modus: Glockenkurve mit Peak, Breite, Min und Max.
    /// RisingPlateau-Modus: Steigt sanft an bis peakDistance, bleibt dann auf maxValue.
    ///   spread bestimmt die Breite der Übergangszone VOR dem Peak.
    ///   Vor (peakDistance - spread): minValue. Ab peakDistance: maxValue.
    /// CustomCurve-Modus: Freie AnimationCurve für beliebige Formen.
    /// </summary>
    [System.Serializable]
    public class DistanceCurve
    {
        [Tooltip("Gaussian = Glockenkurve. RisingPlateau = steigt an und bleibt. CustomCurve = freie AnimationCurve.")]
        public DistributionMode mode = DistributionMode.Gaussian;
        
        [Header("Gaussian / RisingPlateau")]
        [Tooltip("Distanz (in Tiles) wo der Wert sein Maximum erreicht.")]
        public float peakDistance = 250f;
        
        [Tooltip("Gaussian: Standardabweichung der Glockenkurve. RisingPlateau: Breite der Übergangszone VOR dem Peak.")]
        public float spread = 200f;
        
        [Tooltip("Maximaler Wert am Peak der Kurve.")]
        public float maxValue = 1f;
        
        [Tooltip("Minimaler Wert — sinkt nie unter diesen Wert.")]
        [Range(0f, 1f)]
        public float minValue = 0.05f;
        
        [Header("Custom Curve")]
        [Tooltip("X-Achse = Distanz (in Tiles), Y-Achse = Wert. Wird nur benutzt wenn Mode = CustomCurve.")]
        public AnimationCurve customCurve = AnimationCurve.EaseInOut(0f, 0f, 1000f, 1f);
        
        /// <summary>
        /// Wertet die Kurve an einer gegebenen Distanz aus.
        /// </summary>
        /// <param name="distance">Aktuelle Distanz in Tiles (worldX).</param>
        /// <returns>Wert >= minValue.</returns>
        public float Evaluate(float distance)
        {
            switch (mode)
            {
                case DistributionMode.Gaussian:
                    return EvaluateGaussian(distance);
                case DistributionMode.RisingPlateau:
                    return EvaluateRisingPlateau(distance);
                case DistributionMode.CustomCurve:
                    return EvaluateCustomCurve(distance);
                default:
                    return minValue;
            }
        }
        
        /// <summary>
        /// Gauß-Formel: minValue + (maxValue - minValue) * e^(-0.5 * ((x - peak) / spread)^2)
        /// </summary>
        float EvaluateGaussian(float distance)
        {
            float safeSigma = Mathf.Max(spread, 0.001f);
            float exponent = -0.5f * Mathf.Pow((distance - peakDistance) / safeSigma, 2);
            float gaussian = Mathf.Exp(exponent);
            return minValue + (maxValue - minValue) * gaussian;
        }
        
        /// <summary>
        /// RisingPlateau: Sanfter Anstieg von minValue zu maxValue, dann Plateau.
        /// Vor (peakDistance - spread): minValue (flach).
        /// Zwischen (peakDistance - spread) und peakDistance: SmoothStep-Übergang.
        /// Ab peakDistance: maxValue (bleibt dort).
        /// 
        /// Beispiel: peakDistance=1000, spread=500
        ///   0-500:    minValue (nichts spawnt)
        ///   500-1000: sanfter Anstieg
        ///   1000+:    maxValue (volle Stärke)
        /// </summary>
        float EvaluateRisingPlateau(float distance)
        {
            float safeSpread = Mathf.Max(spread, 0.001f);
            float rampStart = peakDistance - safeSpread;
            
            // Vor der Übergangszone: minValue
            if (distance <= rampStart)
                return minValue;
            
            // Nach dem Peak: maxValue
            if (distance >= peakDistance)
                return maxValue;
            
            // In der Übergangszone: SmoothStep (S-Kurve)
            float t = (distance - rampStart) / safeSpread;
            float smooth = t * t * (3f - 2f * t); // Hermite SmoothStep
            return minValue + (maxValue - minValue) * smooth;
        }
        
        float EvaluateCustomCurve(float distance)
        {
            float value = customCurve.Evaluate(distance);
            return Mathf.Max(value, minValue);
        }
    }

    /// <summary>
    /// Ein spawbares Objekt mit distanzbasierter Gewichtung.
    /// Wiederverwendbar für Emeralds, Items, Power-Ups, etc.
    /// 
    /// Nutzt DistanceCurve für die Gewichtsberechnung.
    /// Das minValue der Kurve sorgt dafür, dass ein Item nie komplett verschwindet —
    /// der Spieler hat immer eine kleine Chance, seltene Items zu finden.
    /// </summary>
    [System.Serializable]
    public class SpawnableEntry
    {
        public string name = "Item";
        public GameObject prefab;
        
        [Header("Distance-Based Weight")]
        [Tooltip("Distanzkurve die das Spawn-Gewicht dieses Eintrags bestimmt.")]
        public DistanceCurve weight = new DistanceCurve();
        
        /// <summary>
        /// Berechnet das Spawn-Gewicht für eine gegebene Distanz.
        /// Delegiert an die DistanceCurve.
        /// </summary>
        public float EvaluateWeight(float distance)
        {
            return weight.Evaluate(distance);
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
