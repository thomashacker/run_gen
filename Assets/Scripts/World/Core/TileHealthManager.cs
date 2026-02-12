using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Verwaltet Tile-Health zur Laufzeit.
    /// Tiles starten mit defaultTileHealth und werden bei 0 HP zerstört.
    /// Lazy-Init: Nur getroffene Tiles werden im Dictionary gespeichert.
    /// </summary>
    public class TileHealthManager : MonoBehaviour
    {
        [Header("Tile Health")]
        [Tooltip("Standard-HP für alle zerstörbaren Tiles")]
        public int defaultTileHealth = 3;
        
        [Header("Visual Feedback")]
        [Tooltip("Dauer des Hit-Effekts in Sekunden")]
        public float hitEffectDuration = 0.15f;
        [Tooltip("Maximale Skalierung beim Hit-Effekt")]
        public float hitEffectScale = 1.3f;
        [Tooltip("Start-Alpha des Hit-Effekts")]
        public float hitEffectAlpha = 0.8f;
        
        [Header("Debug")]
        public bool debugMode = false;
        
        // Singleton
        public static TileHealthManager Instance { get; private set; }
        
        // Runtime: World tile position -> remaining health
        private Dictionary<Vector2Int, int> tileHealth = new Dictionary<Vector2Int, int>();
        
        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(this);
                return;
            }
        }
        
        /// <summary>
        /// Fügt einem Tile Schaden zu. Zerstört es wenn HP auf 0 fallen.
        /// Returns true wenn das Tile zerstört wurde.
        /// </summary>
        public bool DamageTile(int worldX, int worldY, int damage = 1)
        {
            ChunkManager chunkManager = ChunkManager.Instance;
            if (chunkManager == null) return false;
            
            ChunkRenderer renderer = chunkManager.chunkRenderer;
            if (renderer == null) return false;
            
            // Prüfen ob an dieser Position überhaupt ein Tile existiert
            Tilemap tilemap = GetTilemapWithTileAt(renderer, worldX, worldY);
            if (tilemap == null)
            {
                if (debugMode) Debug.Log($"[TileHealthManager] No tile at ({worldX}, {worldY})");
                return false;
            }
            
            Vector2Int key = new Vector2Int(worldX, worldY);
            
            // Lazy Init: Beim ersten Treffer Health initialisieren
            if (!tileHealth.ContainsKey(key))
            {
                tileHealth[key] = defaultTileHealth;
            }
            
            // Schaden anwenden
            tileHealth[key] -= damage;
            
            if (debugMode)
                Debug.Log($"[TileHealthManager] Tile ({worldX}, {worldY}) hit! HP: {tileHealth[key]}/{defaultTileHealth}");
            
            if (tileHealth[key] <= 0)
            {
                // Tile zerstören
                DestroyTile(chunkManager, renderer, tilemap, worldX, worldY);
                tileHealth.Remove(key);
                return true;
            }
            else
            {
                // Visuelles Feedback: Hit-Effekt spawnen
                SpawnHitEffect(tilemap, worldX, worldY);
                return false;
            }
        }
        
        /// <summary>
        /// Findet die Tilemap die an dieser World-Position ein Tile hat.
        /// Prüft Ground und Platform Tilemaps.
        /// </summary>
        Tilemap GetTilemapWithTileAt(ChunkRenderer renderer, int worldX, int worldY)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, 0);
            
            // Ground Tilemap prüfen
            if (renderer.groundTilemap != null && renderer.groundTilemap.GetTile(pos) != null)
                return renderer.groundTilemap;
            
            // Platform Tilemap prüfen (falls separat)
            if (renderer.platformTilemap != null && renderer.platformTilemap != renderer.groundTilemap
                && renderer.platformTilemap.GetTile(pos) != null)
                return renderer.platformTilemap;
            
            return null;
        }
        
        /// <summary>
        /// Zerstört ein Tile (gleiche Logik wie Explosion.cs).
        /// </summary>
        void DestroyTile(ChunkManager chunkManager, ChunkRenderer renderer, Tilemap tilemap, int worldX, int worldY)
        {
            Vector3Int tilePos = new Vector3Int(worldX, worldY, 0);
            
            // Tile aus Tilemap entfernen
            tilemap.SetTile(tilePos, null);
            
            // ChunkData aktualisieren
            int chunkIndex = worldX / chunkManager.ChunkWidth;
            if (worldX < 0 && worldX % chunkManager.ChunkWidth != 0)
                chunkIndex--;
            
            ChunkData chunk = chunkManager.GetChunk(chunkIndex);
            if (chunk != null)
            {
                int localX = worldX - (chunkIndex * chunkManager.ChunkWidth);
                if (chunk.IsInBounds(localX, worldY))
                {
                    chunk[localX, worldY] = TileData.Air;
                }
            }
            
            // Collider aktualisieren
            renderer.RefreshColliders();
            
            // Zerstörungs-Effekt (gleicher Hit-Effekt, etwas stärker)
            SpawnHitEffect(tilemap, worldX, worldY);
            
            if (debugMode)
                Debug.Log($"[TileHealthManager] Tile ({worldX}, {worldY}) destroyed!");
        }
        
        /// <summary>
        /// Spawnt den visuellen Hit-Effekt (weißer Flash + Scale Punch).
        /// </summary>
        void SpawnHitEffect(Tilemap tilemap, int worldX, int worldY)
        {
            // Tile-Zentrum in World-Koordinaten berechnen
            Vector3Int cellPos = new Vector3Int(worldX, worldY, 0);
            Vector3 cellCenter = tilemap.GetCellCenterWorld(cellPos);
            
            // Effekt-GameObject erstellen
            GameObject effectObj = new GameObject("TileHitEffect");
            effectObj.transform.position = cellCenter;
            
            TileHitEffect effect = effectObj.AddComponent<TileHitEffect>();
            effect.duration = hitEffectDuration;
            effect.maxScale = hitEffectScale;
            effect.startAlpha = hitEffectAlpha;
            effect.cellSize = tilemap.cellSize;
        }
        
        /// <summary>
        /// Räumt alle Health-Einträge auf (z.B. bei World Regeneration).
        /// </summary>
        public void ClearAllHealth()
        {
            tileHealth.Clear();
        }
        
        /// <summary>
        /// Entfernt Health-Einträge für einen bestimmten Chunk.
        /// </summary>
        public void ClearHealthForChunk(int chunkIndex, int chunkWidth)
        {
            int startX = chunkIndex * chunkWidth;
            int endX = startX + chunkWidth;
            
            List<Vector2Int> toRemove = new List<Vector2Int>();
            foreach (var key in tileHealth.Keys)
            {
                if (key.x >= startX && key.x < endX)
                    toRemove.Add(key);
            }
            
            foreach (var key in toRemove)
                tileHealth.Remove(key);
        }
    }
}
