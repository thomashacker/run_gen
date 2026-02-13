using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Spawnt Enemies auf Ground und Plattformen.
    /// Prüft ob genug Platz vorhanden ist.
    /// 
    /// Nutzt DistanceCurve für distanzbasierte Spawn-Wahrscheinlichkeit:
    /// Jeder Enemy-Typ hat seine eigene Kurve, sodass verschiedene Gegner
    /// in verschiedenen Distanzzonen häufiger oder seltener auftreten.
    /// </summary>
    public class EnemyPass : GeneratorPassBase
    {
        [System.Serializable]
        public class EnemyPrefab
        {
            public string name = "Enemy";
            public GameObject prefab;
            
            [Header("Spawn Probability (Distance-Based)")]
            [Tooltip("Distanzkurve die die Spawn-Wahrscheinlichkeit bestimmt. Der Wert der Kurve wird direkt als Chance verwendet (0-1).")]
            public DistanceCurve spawnProbability = new DistanceCurve
            {
                mode = DistributionMode.Gaussian,
                peakDistance = 500f,
                spread = 300f,
                maxValue = 0.1f,
                minValue = 0.01f
            };
            
            [Header("Spawn On")]
            public bool spawnOnGround = true;
            public bool spawnOnPlatforms = true;
            
            [Header("Space Requirements (in Tiles)")]
            [Tooltip("Freier Platz nach links benötigt")]
            public int spaceLeft = 1;
            [Tooltip("Freier Platz nach rechts benötigt")]
            public int spaceRight = 1;
            [Tooltip("Freier Platz nach oben benötigt")]
            public int spaceUp = 2;
            [Tooltip("Freier Platz oben-links benötigt")]
            public int spaceUpLeft = 1;
            [Tooltip("Freier Platz oben-rechts benötigt")]
            public int spaceUpRight = 1;
            
            [Header("Spawn Limits")]
            public int minDistanceBetween = 5;
            
            [Header("Spawn Offset")]
            [Tooltip("Y-Offset beim Spawnen (um nicht in Plattformen zu stecken)")]
            public float spawnYOffset = 0.5f;
        }
        
        [Header("Enemy Prefabs")]
        public List<EnemyPrefab> enemies = new List<EnemyPrefab>();
        
        [Header("General Settings")]
        [Tooltip("Spalten am Anfang ohne Enemies")]
        public int safeStartColumns = 30;
        [Tooltip("Parent-Objekt für gespawnte Enemies")]
        public Transform enemyParent;
        
        [Header("Debug")]
        public bool showSpawnPositions = false;
        
        // Runtime: Letzte Spawn-Positionen pro Enemy-Typ (für Mindestabstand)
        private Dictionary<int, int> lastSpawnX = new Dictionary<int, int>();
        
        // Liste der gespawnten Enemies (für Cleanup)
        private List<GameObject> spawnedEnemies = new List<GameObject>();
        
        public override void Initialize(GenerationContext context)
        {
            lastSpawnX.Clear();
            
            // Parent erstellen wenn nicht vorhanden
            if (enemyParent == null)
            {
                GameObject parent = new GameObject("Enemies");
                enemyParent = parent.transform;
            }
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            // Ground-Spawns
            if (chunk.metadata.surfaceHeights != null)
            {
                ProcessGroundSpawns(chunk, context);
            }
            
            // Platform-Spawns
            ProcessPlatformSpawns(chunk, context);
            
            return chunk;
        }
        
        void ProcessGroundSpawns(ChunkData chunk, GenerationContext context)
        {
            int[] heights = chunk.metadata.surfaceHeights;
            if (heights == null) return;
            
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                // Safe Start (nutzt ChunkUtilities)
                if (ChunkUtilities.IsInSafeStartZone(worldX, safeStartColumns)) continue;
                
                int groundY = ChunkUtilities.GetGroundHeight(chunk, x, -1);
                if (groundY < 0) continue;
                
                // Für jeden Enemy-Typ prüfen
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy.prefab == null) continue;
                    if (!enemy.spawnOnGround) continue;
                    
                    TrySpawnEnemy(chunk, context, enemy, i, x, groundY, TileLayer.Ground);
                }
            }
        }
        
        void ProcessPlatformSpawns(ChunkData chunk, GenerationContext context)
        {
            // Platform-Tops finden (nutzt ChunkUtilities)
            int[] platformTops = ChunkUtilities.GetPlatformTops(chunk);
            
            // Spawns auf Plattformen
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                // Safe Start (nutzt ChunkUtilities)
                if (ChunkUtilities.IsInSafeStartZone(worldX, safeStartColumns)) continue;
                
                int platformY = platformTops[x];
                if (platformY < 0) continue;
                
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy.prefab == null) continue;
                    if (!enemy.spawnOnPlatforms) continue;
                    
                    TrySpawnEnemy(chunk, context, enemy, i, x, platformY, TileLayer.Platform);
                }
            }
        }
        
        void TrySpawnEnemy(ChunkData chunk, GenerationContext context, EnemyPrefab enemy, 
            int enemyIndex, int localX, int surfaceY, TileLayer layer)
        {
            int worldX = chunk.LocalToWorldX(localX);
            
            // Mindestabstand prüfen
            if (lastSpawnX.TryGetValue(enemyIndex, out int lastX))
            {
                if (Mathf.Abs(worldX - lastX) < enemy.minDistanceBetween)
                    return;
            }
            
            // Distanzbasierte Spawn-Chance
            float spawnChance = enemy.spawnProbability.Evaluate(worldX);
            if (Random.value > spawnChance)
                return;
            
            // Ramp-Check: Nicht auf Rampen spawnen
            if (ChunkUtilities.IsSurfaceRamp(chunk, localX, surfaceY))
                return;
            
            // Position bereits besetzt?
            if (ChunkUtilities.IsPositionOccupied(context, worldX, surfaceY + 1))
                return;
            
            // Platz prüfen
            if (!HasEnoughSpace(chunk, localX, surfaceY, enemy))
                return;
            
            // Spawn-Position berechnen (nutzt ChunkUtilities)
            Vector3 spawnPos = ChunkUtilities.CalculateSpawnPosition(
                chunk, context, localX, surfaceY, enemy.spawnYOffset, centerInTile: false);
            
            // Enemy spawnen
            SpawnEnemy(enemy.prefab, spawnPos);
            
            // Position als besetzt markieren
            ChunkUtilities.OccupyPosition(context, worldX, surfaceY + 1);
            
            if (showSpawnPositions)
            {
                // Alle Enemy-Typen mit ihrer aktuellen Wahrscheinlichkeit anzeigen
                var sb = new System.Text.StringBuilder();
                sb.Append($"[EnemyPass] [Distance={worldX}] ");
                for (int j = 0; j < enemies.Count; j++)
                {
                    if (j > 0) sb.Append(", ");
                    float prob = enemies[j].spawnProbability.Evaluate(worldX);
                    sb.Append($"{enemies[j].name}: {prob:P2}");
                }
                sb.Append($" → Spawned: {enemy.name} at ({worldX}, {surfaceY + 1})");
                Debug.Log(sb.ToString());
            }
            
            // Letzte Position merken
            lastSpawnX[enemyIndex] = worldX;
        }
        
        bool HasEnoughSpace(ChunkData chunk, int x, int surfaceY, EnemyPrefab enemy)
        {
            // Spawn-Position ist bei (x, surfaceY + 1) - also ÜBER der Oberfläche
            int spawnY = surfaceY + 1;
            
            // Links prüfen
            for (int dx = 1; dx <= enemy.spaceLeft; dx++)
            {
                if (!IsFreeAt(chunk, x - dx, spawnY))
                    return false;
            }
            
            // Rechts prüfen
            for (int dx = 1; dx <= enemy.spaceRight; dx++)
            {
                if (!IsFreeAt(chunk, x + dx, spawnY))
                    return false;
            }
            
            // Oben prüfen
            for (int dy = 0; dy < enemy.spaceUp; dy++)
            {
                if (!IsFreeAt(chunk, x, spawnY + dy))
                    return false;
            }
            
            // Oben-Links prüfen
            for (int d = 1; d <= enemy.spaceUpLeft; d++)
            {
                if (!IsFreeAt(chunk, x - d, spawnY + d))
                    return false;
            }
            
            // Oben-Rechts prüfen
            for (int d = 1; d <= enemy.spaceUpRight; d++)
            {
                if (!IsFreeAt(chunk, x + d, spawnY + d))
                    return false;
            }
            
            return true;
        }
        
        bool IsFreeAt(ChunkData chunk, int x, int y)
        {
            // Nutzt ChunkUtilities (Außerhalb = frei)
            return ChunkUtilities.IsEmpty(chunk, x, y);
        }
        
        void SpawnEnemy(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            
            GameObject enemy = Object.Instantiate(prefab, position, Quaternion.identity, enemyParent);
            spawnedEnemies.Add(enemy);
            
            if (showSpawnPositions)
            {
                Debug.Log($"[EnemyPass] Spawned {prefab.name} at {position}");
            }
        }
        
        /// <summary>
        /// Entfernt alle gespawnten Enemies (für Reset/Cleanup)
        /// </summary>
        public void ClearAllEnemies()
        {
            foreach (var enemy in spawnedEnemies)
            {
                if (enemy != null)
                    Object.Destroy(enemy);
            }
            spawnedEnemies.Clear();
            lastSpawnX.Clear();
        }
        
        void OnDestroy()
        {
            ClearAllEnemies();
        }
        
        void Reset()
        {
            passName = "Enemy Pass";
            
            if (enemies.Count == 0)
            {
                enemies.Add(new EnemyPrefab
                {
                    name = "Basic Enemy",
                    spawnProbability = new DistanceCurve
                    {
                        mode = DistributionMode.Gaussian,
                        peakDistance = 500f,
                        spread = 300f,
                        maxValue = 0.05f,
                        minValue = 0.005f
                    },
                    spaceLeft = 1,
                    spaceRight = 1,
                    spaceUp = 2,
                    spaceUpLeft = 1,
                    spaceUpRight = 1,
                    minDistanceBetween = 10,
                    spawnYOffset = 0.5f
                });
            }
        }
    }
}
