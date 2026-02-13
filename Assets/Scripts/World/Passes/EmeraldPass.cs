using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Spawnt Linien von Rubinen/Münzen auf Ground und Plattformen.
    /// Emeralds folgen der Oberfläche (Ground oder Platform).
    /// 
    /// Nutzt das SpawnDistribution-System für distanzbasierte Auswahl:
    /// Jeder Emerald-Typ hat eine eigene Spawn-Kurve (Gaussian oder Custom),
    /// sodass verschiedene Typen in verschiedenen Distanzzonen dominieren,
    /// aber nie komplett verschwinden.
    /// </summary>
    public class EmeraldPass : GeneratorPassBase
    {
        [Header("Emerald Types (Distance-Based)")]
        [Tooltip("Jeder Eintrag hat seine eigene Distanzkurve. Emerald-Typ wird pro Linie basierend auf worldX ausgewählt.")]
        public List<SpawnableEntry> emeraldEntries = new List<SpawnableEntry>();
        
        [Header("Line Settings")]
        [Range(0f, 1f)]
        [Tooltip("Wahrscheinlichkeit dass eine Linie startet")]
        public float lineSpawnChance = 0.1f;
        
        [Tooltip("Minimale Länge der Linie (Anzahl Rubine)")]
        public int minLength = 3;
        [Tooltip("Maximale Länge der Linie")]
        public int maxLength = 8;
        
        [Header("Positioning")]
        [Tooltip("Zusätzliche Höhe über der Oberfläche (0 = zentriert im Tile direkt über Ground)")]
        public float heightAboveSurface = 0f;
        
        [Header("General")]
        [Tooltip("Minimaler Abstand zwischen Linien (in Tiles)")]
        public int minDistanceBetweenLines = 10;
        [Tooltip("Spalten am Anfang ohne Rubine")]
        public int safeStartColumns = 20;
        [Tooltip("Spawn auf Ground")]
        public bool spawnOnGround = true;
        [Tooltip("Spawn auf Plattformen")]
        public bool spawnOnPlatforms = true;
        
        [Header("Parent")]
        public Transform emeraldParent;
        
        [Header("Debug")]
        public bool debugMode = false;
        
        // Runtime
        private int lastLineEndX = -999;
        private List<GameObject> spawnedEmeralds = new List<GameObject>();
        
        public override void Initialize(GenerationContext context)
        {
            lastLineEndX = -999;
            
            if (emeraldParent == null)
            {
                GameObject parent = new GameObject("Emeralds");
                emeraldParent = parent.transform;
            }
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            if (emeraldEntries.Count == 0) return chunk;
            
            // Oberflächen-Map erstellen (nutzt ChunkUtilities)
            int[] surfaceHeights = ChunkUtilities.GetCombinedSurfaceHeights(chunk, spawnOnGround, spawnOnPlatforms);
            
            // Linien spawnen
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                // Safe Start (nutzt ChunkUtilities)
                if (ChunkUtilities.IsInSafeStartZone(worldX, safeStartColumns)) continue;
                
                // Mindestabstand zur letzten Linie
                if (worldX < lastLineEndX + minDistanceBetweenLines) continue;
                
                // Spawn-Chance
                if (Random.value > lineSpawnChance) continue;
                
                // Oberfläche an dieser Position
                int surfaceY = surfaceHeights[x];
                if (surfaceY < 0) continue;
                
                // Linie spawnen
                int lineLength = Random.Range(minLength, maxLength + 1);
                SpawnLine(chunk, context, x, lineLength, surfaceHeights);
                
                lastLineEndX = worldX + lineLength;
                
                // Skip nach Linie
                x += lineLength;
            }
            
            return chunk;
        }
        
        void SpawnLine(ChunkData chunk, GenerationContext context, int startLocalX, int length, int[] surfaceHeights)
        {
            // Distanzbasierte Auswahl: worldX bestimmt welcher Emerald-Typ gespawnt wird
            int lineWorldX = chunk.LocalToWorldX(startLocalX);
            SpawnableEntry selectedEntry = SpawnDistribution.SelectWeighted(emeraldEntries, lineWorldX);
            if (selectedEntry == null || selectedEntry.prefab == null) return;
            
            if (debugMode)
            {
                Debug.Log($"[EmeraldPass] {SpawnDistribution.GetDebugString(emeraldEntries, lineWorldX)} → Selected: {selectedEntry.name}");
            }
            
            for (int i = 0; i < length; i++)
            {
                int localX = startLocalX + i;
                
                // Bounds check
                if (localX >= chunk.width) break;
                
                int surfaceY = surfaceHeights[localX];
                
                // Kein Boden/Platform? Skip (Gap)
                if (surfaceY < 0) continue;
                
                // Ramp-Check: Nicht über Rampen spawnen
                if (ChunkUtilities.IsSurfaceRamp(chunk, localX, surfaceY))
                {
                    if (debugMode) Debug.Log($"[EmeraldPass] Skipped ({localX}, {surfaceY}) - Ramp detected");
                    continue;
                }
                
                // Position bereits besetzt? (z.B. durch Enemy)
                int worldX = chunk.LocalToWorldX(localX);
                if (ChunkUtilities.IsPositionOccupied(context, worldX, surfaceY + 1))
                {
                    if (debugMode) Debug.Log($"[EmeraldPass] Skipped ({worldX}, {surfaceY + 1}) - Position occupied");
                    continue;
                }
                
                // World Position (nutzt ChunkUtilities)
                Vector3 spawnPos = ChunkUtilities.CalculateSpawnPosition(
                    chunk, context, localX, surfaceY, heightAboveSurface, centerInTile: false);
                
                GameObject emerald = Object.Instantiate(selectedEntry.prefab, spawnPos, Quaternion.identity, emeraldParent);
                spawnedEmeralds.Add(emerald);
                
                // Position als besetzt markieren
                ChunkUtilities.OccupyPosition(context, worldX, surfaceY + 1);
            }
        }
        
        // Emerald-Auswahl läuft jetzt über SpawnDistribution.SelectWeighted() in SpawnLine().
        
        public void ClearAllEmeralds()
        {
            foreach (var emerald in spawnedEmeralds)
            {
                if (emerald != null)
                    Object.Destroy(emerald);
            }
            spawnedEmeralds.Clear();
        }
        
        void OnDestroy()
        {
            ClearAllEmeralds();
        }
        
        void Reset()
        {
            passName = "Emerald Pass";
            
            if (emeraldEntries.Count == 0)
            {
                // Standard-Setup: Green Emerald früh, Blue Emerald spät
                emeraldEntries.Add(new SpawnableEntry
                {
                    name = "Green Emerald",
                    mode = DistributionMode.Gaussian,
                    peakDistance = 250f,
                    spread = 200f,
                    maxWeight = 1f,
                    minWeight = 0.05f
                });
                
                emeraldEntries.Add(new SpawnableEntry
                {
                    name = "Blue Emerald",
                    mode = DistributionMode.Gaussian,
                    peakDistance = 1000f,
                    spread = 400f,
                    maxWeight = 1f,
                    minWeight = 0.05f
                });
            }
        }
    }
}
