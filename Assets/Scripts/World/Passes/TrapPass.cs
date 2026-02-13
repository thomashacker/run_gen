using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Spawnt Fallen (Traps) auf Ground und Plattformen.
    /// Nutzt DistanceCurve für distanzbasierte Spawn-Wahrscheinlichkeit.
    /// Traps werden auf der Oberfläche platziert (wie Emeralds/Enemies).
    /// </summary>
    public class TrapPass : GeneratorPassBase
    {
        [System.Serializable]
        public class TrapEntry
        {
            public string name = "Trap";
            public GameObject prefab;
            
            [Header("Spawn Probability (Distance-Based)")]
            [Tooltip("Distanzkurve die die Spawn-Wahrscheinlichkeit bestimmt. Der Wert der Kurve wird direkt als Chance verwendet (0-1).")]
            public DistanceCurve spawnProbability = new DistanceCurve
            {
                mode = DistributionMode.Gaussian,
                peakDistance = 300f,
                spread = 200f,
                maxValue = 0.05f,
                minValue = 0.005f
            };
            
            [Header("Spawn On")]
            public bool spawnOnGround = true;
            public bool spawnOnPlatforms = false;
            
            [Header("Ground Requirement")]
            [Tooltip("Benötigt soliden Boden direkt unter der Spawn-Position")]
            public bool requiresGround = true;
            
            [Header("Space Requirements (in Tiles)")]
            [Tooltip("Freier Platz nach links benötigt")]
            public int spaceLeft = 0;
            [Tooltip("Freier Platz nach rechts benötigt")]
            public int spaceRight = 0;
            [Tooltip("Freier Platz nach oben benötigt")]
            public int spaceUp = 0;
            [Tooltip("Freier Platz nach unten benötigt (z.B. für hängende Fallen wie Pendel-Äxte)")]
            public int spaceDown = 0;
            
            [Header("Spawn Limits")]
            [Tooltip("Minimaler Abstand zwischen Traps dieses Typs (in Tiles)")]
            public int minDistanceBetween = 8;
            
            [Header("Spawn Offset")]
            [Tooltip("X-Offset in World-Units (0 = Mitte des Tiles)")]
            public float spawnXOffset = 0f;
            [Tooltip("Y-Offset in World-Units (0 = Unterkante des Traps auf der Oberfläche)")]
            public float spawnYOffset = 0f;
        }
        
        [Header("Trap Types")]
        public List<TrapEntry> traps = new List<TrapEntry>();
        
        [Header("General Settings")]
        [Tooltip("Spalten am Anfang ohne Traps")]
        public int safeStartColumns = 30;
        [Tooltip("Parent-Objekt für gespawnte Traps")]
        public Transform trapParent;
        
        [Header("Debug")]
        public bool showSpawnPositions = false;
        
        // Runtime: Letzte Spawn-Position pro Trap-Typ (für Mindestabstand)
        private Dictionary<int, int> lastSpawnX = new Dictionary<int, int>();
        
        // Liste der gespawnten Traps (für Cleanup)
        private List<GameObject> spawnedTraps = new List<GameObject>();
        
        public override void Initialize(GenerationContext context)
        {
            lastSpawnX.Clear();
            
            if (trapParent == null)
            {
                GameObject parent = new GameObject("Traps");
                trapParent = parent.transform;
            }
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            if (traps.Count == 0) return chunk;
            
            // Für jeden Trap-Typ eigene Oberflächen-Map berechnen basierend auf spawnOnGround/spawnOnPlatforms.
            // Nutzt GetCombinedSurfaceHeights welches die TATSÄCHLICHEN Tiles scannt (nach Cave/Gap-Carving),
            // nicht die veralteten metadata.surfaceHeights vom GroundPass.
            for (int i = 0; i < traps.Count; i++)
            {
                var trap = traps[i];
                if (trap.prefab == null) continue;
                
                int[] surfaceHeights = ChunkUtilities.GetCombinedSurfaceHeights(
                    chunk, trap.spawnOnGround, trap.spawnOnPlatforms);
                
                for (int x = 0; x < chunk.width; x++)
                {
                    int worldX = chunk.LocalToWorldX(x);
                    
                    if (ChunkUtilities.IsInSafeStartZone(worldX, safeStartColumns)) continue;
                    
                    int surfaceY = surfaceHeights[x];
                    if (surfaceY < 0) continue;
                    
                    TrySpawnTrap(chunk, context, trap, i, x, surfaceY);
                }
            }
            
            return chunk;
        }
        
        void TrySpawnTrap(ChunkData chunk, GenerationContext context, TrapEntry trap,
            int trapIndex, int localX, int surfaceY)
        {
            int worldX = chunk.LocalToWorldX(localX);
            
            // Spalte bereits von einem anderen Entity belegt? (Emerald, Enemy, anderer Trap)
            if (ChunkUtilities.IsColumnOccupied(context, worldX))
                return;
            
            // Mindestabstand prüfen
            if (lastSpawnX.TryGetValue(trapIndex, out int lastX))
            {
                if (Mathf.Abs(worldX - lastX) < trap.minDistanceBetween)
                    return;
            }
            
            // Distanzbasierte Spawn-Chance
            float spawnChance = trap.spawnProbability.Evaluate(worldX);
            if (Random.value > spawnChance)
                return;
            
            // Nicht auf Rampen spawnen
            if (ChunkUtilities.IsSurfaceRamp(chunk, localX, surfaceY))
                return;
            
            // Boden-Anforderung: Solides Tile direkt unter der Spawn-Position?
            if (trap.requiresGround && !ChunkUtilities.IsSolid(chunk, localX, surfaceY))
                return;
            
            // Position bereits besetzt?
            if (ChunkUtilities.IsPositionOccupied(context, worldX, surfaceY + 1))
                return;
            
            // Platz-Anforderungen prüfen (Tiles + Entities)
            if (!HasEnoughSpace(chunk, context, localX, surfaceY, trap))
                return;
            
            // Spawn-Position direkt berechnen (nicht über CalculateSpawnPosition,
            // da dessen +1.5f Offset für schwebende Items gedacht ist, nicht für Boden-Traps).
            // surfaceY = Y des obersten soliden Tiles (aus Tile-Scan).
            // (surfaceY + 1) * cellSize.y = Oberkante dieses Tiles = Boden-Level.
            int worldTileX = chunk.LocalToWorldX(localX);
            float posX = (worldTileX + 0.5f) * context.cellSize.x + trap.spawnXOffset;  // Tile-Mitte + Offset
            float posY = (surfaceY + 1) * context.cellSize.y + trap.spawnYOffset;        // Oberkante Surface + Offset
            Vector3 spawnPos = new Vector3(posX, posY, 0);
            
            // Trap spawnen
            GameObject trapObj = Object.Instantiate(trap.prefab, spawnPos, Quaternion.identity, trapParent);
            spawnedTraps.Add(trapObj);
            
            // Position und Spalte als besetzt markieren
            ChunkUtilities.OccupyPosition(context, worldX, surfaceY + 1);
            ChunkUtilities.OccupyColumn(context, worldX);
            
            if (showSpawnPositions)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[TrapPass] [Distance={worldX}] ");
                for (int j = 0; j < traps.Count; j++)
                {
                    if (j > 0) sb.Append(", ");
                    float prob = traps[j].spawnProbability.Evaluate(worldX);
                    sb.Append($"{traps[j].name}: {prob:P2}");
                }
                sb.Append($" → Spawned: {trap.name} at ({worldX}, {surfaceY + 1})");
                Debug.Log(sb.ToString());
            }
            
            // Letzte Position merken
            lastSpawnX[trapIndex] = worldX;
        }
        
        /// <summary>
        /// Prüft ob genug freier Platz um die Spawn-Position ist.
        /// Prüft sowohl Tiles (leerer Raum) als auch belegte Spalten (andere Entities).
        /// </summary>
        bool HasEnoughSpace(ChunkData chunk, GenerationContext context, int localX, int surfaceY, TrapEntry trap)
        {
            int spawnY = surfaceY + 1;
            int worldX = chunk.LocalToWorldX(localX);
            
            // Links prüfen
            for (int dx = 1; dx <= trap.spaceLeft; dx++)
            {
                if (!ChunkUtilities.IsEmpty(chunk, localX - dx, spawnY))
                    return false;
                if (ChunkUtilities.IsColumnOccupied(context, worldX - dx))
                    return false;
            }
            
            // Rechts prüfen
            for (int dx = 1; dx <= trap.spaceRight; dx++)
            {
                if (!ChunkUtilities.IsEmpty(chunk, localX + dx, spawnY))
                    return false;
                if (ChunkUtilities.IsColumnOccupied(context, worldX + dx))
                    return false;
            }
            
            // Oben prüfen
            for (int dy = 0; dy < trap.spaceUp; dy++)
            {
                if (!ChunkUtilities.IsEmpty(chunk, localX, spawnY + dy))
                    return false;
            }
            
            // Unten prüfen (unter der Oberfläche, z.B. für hängende Fallen)
            for (int dy = 1; dy <= trap.spaceDown; dy++)
            {
                if (!ChunkUtilities.IsEmpty(chunk, localX, spawnY - dy))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Entfernt alle gespawnten Traps (für Reset/Cleanup)
        /// </summary>
        public void ClearAllTraps()
        {
            foreach (var trap in spawnedTraps)
            {
                if (trap != null)
                    Object.Destroy(trap);
            }
            spawnedTraps.Clear();
            lastSpawnX.Clear();
        }
        
        void OnDestroy()
        {
            ClearAllTraps();
        }
        
        void Reset()
        {
            passName = "Trap Pass";
            
            if (traps.Count == 0)
            {
                traps.Add(new TrapEntry
                {
                    name = "Spike Trap",
                    spawnProbability = new DistanceCurve
                    {
                        mode = DistributionMode.RisingPlateau,
                        peakDistance = 300f,
                        spread = 200f,
                        maxValue = 0.03f,
                        minValue = 0.005f
                    },
                    spawnOnGround = true,
                    spawnOnPlatforms = false,
                    requiresGround = true,
                    spaceLeft = 0,
                    spaceRight = 0,
                    spaceUp = 0,
                    spaceDown = 0,
                    minDistanceBetween = 8,
                    spawnXOffset = 0f,
                    spawnYOffset = 0f
                });
            }
        }
    }
}
