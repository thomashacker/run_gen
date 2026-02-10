using UnityEngine;
using UnityEngine.Tilemaps;

namespace WorldGeneration
{
    /// <summary>
    /// Rendert ChunkData zu Unity Tilemaps.
    /// Verwaltet die Tile-Zuweisungen für verschiedene TileTypes und Layer.
    /// </summary>
    public class ChunkRenderer : MonoBehaviour
    {
        [Header("Tilemaps")]
        [Tooltip("Haupt-Tilemap für Ground")]
        public Tilemap groundTilemap;
        [Tooltip("Tilemap für Plattformen (optional, sonst Ground)")]
        public Tilemap platformTilemap;
        [Tooltip("Tilemap für Hintergrund (optional)")]
        public Tilemap backgroundTilemap;
        [Tooltip("Tilemap für Vordergrund-Overlay (z. B. Schatten). Wird an jeder Stelle befüllt, wo ein Hintergrund-Tile gesetzt wird.")]
        public Tilemap foregroundTilemap;
        
        [Header("Ground Tiles")]
        public TileBase groundTile;
        public TileBase groundRampUpTile;
        public TileBase groundRampDownTile;
        
        [Header("Platform Tiles")]
        public TileBase platformTile;
        public TileBase platformRampUpTile;
        public TileBase platformRampDownTile;
        
        [Header("Background Tiles")]
        [Tooltip("Maps BackgroundType to TileBase. Index = (int)BackgroundType (None=0, Default=1, Dirt=2, etc.). ChunkRenderer assigns tiles from this; passes only set the type in data.")]
        public TileBase[] backgroundTilesByType;
        [Tooltip("Fallback when type is None or index is out of range")]
        public TileBase defaultBackgroundTile;
        [Tooltip("Schatten-Tile (z. B. schwarz mit Transparenz). Wird auf foregroundTilemap gesetzt, wo immer ein Hintergrund-Tile ist.")]
        public TileBase shadowTile;
        
        [Header("Settings")]
        [Tooltip("Tilemap für Plattformen wenn keine separate zugewiesen")]
        public bool useSeparatePlatformTilemap = false;
        
        void Awake()
        {
            // Auto-find Tilemaps wenn nicht zugewiesen
            if (groundTilemap == null)
            {
                groundTilemap = GetComponent<Tilemap>();
                if (groundTilemap == null)
                    groundTilemap = GetComponentInChildren<Tilemap>();
            }
            
            // Platform Tilemap fallback
            if (platformTilemap == null && useSeparatePlatformTilemap)
            {
                // Versuche eine mit "Platform" im Namen zu finden
                foreach (var tm in GetComponentsInChildren<Tilemap>())
                {
                    if (tm.name.ToLower().Contains("platform"))
                    {
                        platformTilemap = tm;
                        break;
                    }
                }
            }
            
            if (platformTilemap == null)
                platformTilemap = groundTilemap;
        }
        
        /// <summary>
        /// Rendert einen kompletten Chunk zur Tilemap.
        /// Hintergrund wird zuerst gerendert (hinter allem), dann Foreground (Ground/Platform).
        /// </summary>
        public void RenderChunk(ChunkData chunk)
        {
            // 1) Hintergrund-Matrix zuerst (hinter allem) – Daten haben nur den Typ, wir weisen die echten Tiles zu
            if (backgroundTilemap != null)
            {
                for (int x = 0; x < chunk.width; x++)
                {
                    int worldX = chunk.LocalToWorldX(x);
                    for (int y = 0; y < chunk.height; y++)
                    {
                        TileData bg = chunk.GetBackgroundTile(x, y);
                        if (bg.IsEmpty) continue;
                        TileBase tile = GetBackgroundTileForType(bg.backgroundType);
                        if (tile != null)
                        {
                            Vector3Int pos = new Vector3Int(worldX, y, 0);
                            backgroundTilemap.SetTile(pos, tile);
                            if (foregroundTilemap != null && shadowTile != null)
                                foregroundTilemap.SetTile(pos, shadowTile);
                        }
                    }
                }
            }
            
            // 2) Foreground (Ground, Platform, etc.)
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                for (int y = 0; y < chunk.height; y++)
                {
                    TileData data = chunk[x, y];
                    SetTile(worldX, y, data);
                }
            }
        }
        
        /// <summary>
        /// Entfernt alle Tiles eines Chunks.
        /// </summary>
        public void ClearChunk(ChunkData chunk)
        {
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                for (int y = 0; y < chunk.height; y++)
                {
                    ClearTile(worldX, y);
                }
            }
        }
        
        /// <summary>
        /// Setzt ein einzelnes Tile basierend auf TileData.
        /// </summary>
        public void SetTile(int worldX, int worldY, TileData data)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, 0);
            
            // Welche Tilemap?
            Tilemap targetMap = GetTilemapForLayer(data.layer);
            
            // Welches Tile?
            TileBase tile = GetTileForData(data);
            
            // Override Tile hat Priorität
            if (data.overrideTile != null)
                tile = data.overrideTile;
            
            // Setzen (oder löschen wenn null/Air)
            if (tile != null && !data.IsEmpty)
            {
                targetMap.SetTile(pos, tile);
            }
            else
            {
                // Bei Air/Gap nur Foreground (Ground/Platform) clearen – Background-Matrix bleibt unberührt
                ClearForegroundTile(worldX, worldY);
            }
        }
        
        /// <summary>
        /// Löscht nur Ground- und Platform-Tilemap an einer Position (für Air/Gap im Haupt-Layer).
        /// Hintergrund-Tilemap wird nicht angefasst, damit die Background-Matrix sichtbar bleibt.
        /// </summary>
        void ClearForegroundTile(int worldX, int worldY)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, 0);
            if (groundTilemap != null)
            {
                groundTilemap.SetTile(pos, null);
                groundTilemap.RefreshTile(pos);
            }
            if (platformTilemap != null && platformTilemap != groundTilemap)
            {
                platformTilemap.SetTile(pos, null);
                platformTilemap.RefreshTile(pos);
            }
        }
        
        /// <summary>
        /// Löscht Tiles an einer Position auf allen Tilemaps (z. B. beim Entfernen eines Chunks).
        /// </summary>
        public void ClearTile(int worldX, int worldY, bool debug = false)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, 0);
            
            if (groundTilemap != null)
            {
                var existingTile = groundTilemap.GetTile(pos);
                if (debug && existingTile != null)
                {
                    Debug.Log($"[ChunkRenderer] Clearing tile at {pos}, was: {existingTile.name}");
                }
                
                groundTilemap.SetTile(pos, null);
                groundTilemap.RefreshTile(pos);
            }
            
            if (platformTilemap != null && platformTilemap != groundTilemap)
            {
                platformTilemap.SetTile(pos, null);
                platformTilemap.RefreshTile(pos);
            }
            
            if (backgroundTilemap != null)
            {
                backgroundTilemap.SetTile(pos, null);
                backgroundTilemap.RefreshTile(pos);
            }
            if (foregroundTilemap != null)
            {
                foregroundTilemap.SetTile(pos, null);
                foregroundTilemap.RefreshTile(pos);
            }
        }
        
        /// <summary>
        /// Aktualisiert alle Tilemap-Collider (nach dynamischen Änderungen).
        /// </summary>
        public void RefreshColliders()
        {
            RefreshTilemapCollider(groundTilemap);
            
            if (platformTilemap != null && platformTilemap != groundTilemap)
                RefreshTilemapCollider(platformTilemap);
        }
        
        void RefreshTilemapCollider(Tilemap tilemap)
        {
            if (tilemap == null) return;
            
            // TilemapCollider2D sofort aktualisieren (ProcessTilemapChanges)
            var tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
            if (tilemapCollider != null && tilemapCollider.enabled)
            {
                tilemapCollider.ProcessTilemapChanges();
            }
            
            // CompositeCollider2D refreshen (falls vorhanden)
            var compositeCollider = tilemap.GetComponent<CompositeCollider2D>();
            if (compositeCollider != null)
            {
                compositeCollider.GenerateGeometry();
            }
        }
        
        /// <summary>
        /// Wählt die richtige Tilemap für einen Layer.
        /// </summary>
        Tilemap GetTilemapForLayer(TileLayer layer)
        {
            switch (layer)
            {
                case TileLayer.Platform:
                    return platformTilemap ?? groundTilemap;
                case TileLayer.Background:
                    return backgroundTilemap ?? groundTilemap;
                case TileLayer.Ground:
                default:
                    return groundTilemap;
            }
        }
        
        /// <summary>
        /// Wählt das richtige TileBase für TileData.
        /// </summary>
        TileBase GetTileForData(TileData data)
        {
            bool isPlatform = data.layer == TileLayer.Platform;
            
            switch (data.type)
            {
                case TileType.Solid:
                case TileType.Platform:
                    return isPlatform ? (platformTile ?? groundTile) : groundTile;
                    
                case TileType.RampUp:
                    return isPlatform ? (platformRampUpTile ?? groundRampUpTile) : groundRampUpTile;
                    
                case TileType.RampDown:
                    return isPlatform ? (platformRampDownTile ?? groundRampDownTile) : groundRampDownTile;
                    
                case TileType.Air:
                case TileType.Gap:
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Weist den Hintergrund-Typ aus den Daten einem TileBase zu (oder später: Spawn).
        /// </summary>
        TileBase GetBackgroundTileForType(BackgroundType backgroundType)
        {
            if (backgroundType == BackgroundType.None)
                return null;
            if (backgroundTilesByType == null || backgroundTilesByType.Length == 0)
                return defaultBackgroundTile;
            int i = (int)backgroundType;
            if (i < 0 || i >= backgroundTilesByType.Length)
                return defaultBackgroundTile;
            return backgroundTilesByType[i] != null ? backgroundTilesByType[i] : defaultBackgroundTile;
        }
        
        /// <summary>
        /// Aktualisiert ein einzelnes Tile nach Pass-Modifikation.
        /// </summary>
        public void UpdateTile(ChunkData chunk, int localX, int localY)
        {
            int worldX = chunk.LocalToWorldX(localX);
            TileData data = chunk[localX, localY];
            SetTile(worldX, localY, data);
        }
    }
}
