using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Statische Utility-Methoden für ChunkData und Passes.
    /// Vermeidet Code-Duplikation zwischen verschiedenen Passes.
    /// </summary>
    public static class ChunkUtilities
    {
        // Key für besetzte Positionen im Context
        private const string OCCUPIED_POSITIONS_KEY = "OccupiedPositions";
        
        #region Position Occupation (Entity-Kollisionsprüfung)
        
        /// <summary>
        /// Holt oder erstellt das Set der besetzten Positionen.
        /// </summary>
        private static HashSet<Vector2Int> GetOccupiedPositions(GenerationContext context)
        {
            var positions = context.GetData<HashSet<Vector2Int>>(OCCUPIED_POSITIONS_KEY, null);
            if (positions == null)
            {
                positions = new HashSet<Vector2Int>();
                context.SetData(OCCUPIED_POSITIONS_KEY, positions);
            }
            return positions;
        }
        
        /// <summary>
        /// Markiert eine World-Position als besetzt (für Enemies, Emeralds, etc.).
        /// </summary>
        public static void OccupyPosition(GenerationContext context, int worldX, int worldY)
        {
            GetOccupiedPositions(context).Add(new Vector2Int(worldX, worldY));
        }
        
        /// <summary>
        /// Prüft ob eine World-Position bereits von einem Entity besetzt ist.
        /// </summary>
        public static bool IsPositionOccupied(GenerationContext context, int worldX, int worldY)
        {
            return GetOccupiedPositions(context).Contains(new Vector2Int(worldX, worldY));
        }
        
        /// <summary>
        /// Prüft ob ein Bereich frei von Entities ist.
        /// </summary>
        public static bool IsAreaOccupied(GenerationContext context, int worldX, int worldY, int radius)
        {
            var positions = GetOccupiedPositions(context);
            
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (positions.Contains(new Vector2Int(worldX + dx, worldY + dy)))
                        return true;
                }
            }
            return false;
        }
        
        #endregion
        
        #region Ramp Detection
        
        /// <summary>
        /// Prüft ob an einer Position eine Ramp ist.
        /// </summary>
        public static bool IsRampAt(ChunkData chunk, int localX, int localY)
        {
            if (!chunk.IsInBounds(localX, localY)) return false;
            return chunk[localX, localY].IsRamp;
        }
        
        /// <summary>
        /// Prüft ob an oder über der Oberfläche eine Ramp ist.
        /// Prüft surfaceY und die 2 Tiles darüber.
        /// </summary>
        public static bool IsSurfaceRamp(ChunkData chunk, int localX, int surfaceY)
        {
            // Prüfe bei surfaceY und die Tiles darüber (Ramps können über dem Ground sein)
            for (int y = surfaceY; y <= surfaceY + 2; y++)
            {
                if (IsRampAt(chunk, localX, y))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Prüft ob irgendwo in der Spalte eine Ramp ist (bis maxY).
        /// </summary>
        public static bool HasRampInColumn(ChunkData chunk, int localX, int fromY, int toY)
        {
            for (int y = fromY; y <= toY; y++)
            {
                if (IsRampAt(chunk, localX, y))
                    return true;
            }
            return false;
        }
        
        #endregion

        #region Surface Height Queries
        
        /// <summary>
        /// Holt die Ground-Oberflächen-Höhe für eine lokale X-Position.
        /// </summary>
        public static int GetGroundHeight(ChunkData chunk, int localX, int fallback = -1)
        {
            if (chunk.metadata.surfaceHeights != null && 
                localX >= 0 && localX < chunk.metadata.surfaceHeights.Length)
            {
                return chunk.metadata.surfaceHeights[localX];
            }
            
            int height = chunk.GetSurfaceHeight(localX);
            return height >= 0 ? height : fallback;
        }
        
        /// <summary>
        /// Findet die Platform-Top-Höhen für alle Spalten im Chunk.
        /// Scannt von oben nach unten und findet das erste Platform-Tile.
        /// </summary>
        public static int[] GetPlatformTops(ChunkData chunk)
        {
            int[] platformTops = new int[chunk.width];
            for (int x = 0; x < chunk.width; x++) 
                platformTops[x] = -1;
            
            for (int y = chunk.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < chunk.width; x++)
                {
                    if (platformTops[x] != -1) continue;
                    
                    TileData tile = chunk[x, y];
                    if (tile.layer == TileLayer.Platform && tile.IsSolid)
                    {
                        platformTops[x] = y;
                    }
                }
            }
            
            return platformTops;
        }
        
        /// <summary>
        /// Findet die höchste begehbare Oberfläche (Ground ODER Platform) pro Spalte.
        /// Nützlich für Entity-Spawning.
        /// </summary>
        public static int[] GetCombinedSurfaceHeights(ChunkData chunk, bool includeGround = true, bool includePlatforms = true)
        {
            int[] heights = new int[chunk.width];
            
            for (int x = 0; x < chunk.width; x++)
            {
                heights[x] = -1;
                
                // Von oben nach unten - erste solide Oberfläche
                for (int y = chunk.height - 1; y >= 0; y--)
                {
                    TileData tile = chunk[x, y];
                    
                    if (includePlatforms && tile.layer == TileLayer.Platform && tile.IsSolid)
                    {
                        heights[x] = y;
                        break;
                    }
                    
                    if (includeGround && tile.layer == TileLayer.Ground && tile.IsSolid)
                    {
                        heights[x] = y;
                        break;
                    }
                }
                
                // Fallback: Ground Metadata
                if (heights[x] < 0 && includeGround && chunk.metadata.surfaceHeights != null)
                {
                    if (x < chunk.metadata.surfaceHeights.Length)
                    {
                        heights[x] = chunk.metadata.surfaceHeights[x];
                    }
                }
            }
            
            return heights;
        }
        
        /// <summary>
        /// Findet die maximale Ground-Höhe in einem Bereich um localX.
        /// Nützlich für Platform-Spawning um Überlappung zu vermeiden.
        /// </summary>
        public static int GetMaxGroundHeightInRange(ChunkData chunk, int localX, int lookBehind, int lookAhead)
        {
            if (chunk.metadata.surfaceHeights == null) return 3;
            
            int maxHeight = 0;
            
            for (int i = -lookBehind; i <= lookAhead; i++)
            {
                int checkX = localX + i;
                if (checkX >= 0 && checkX < chunk.metadata.surfaceHeights.Length)
                {
                    maxHeight = Mathf.Max(maxHeight, chunk.metadata.surfaceHeights[checkX]);
                }
            }
            
            return maxHeight;
        }
        
        #endregion
        
        #region Neighbor Queries
        
        /// <summary>
        /// Holt die rechte Kanten-Höhe des linken Nachbar-Chunks.
        /// Für nahtlose Terrain-Übergänge.
        /// </summary>
        public static int GetLeftNeighborHeight(GenerationContext context, int fallback = -1)
        {
            if (context.leftNeighbor != null && context.leftNeighbor.metadata.isComplete)
            {
                return context.leftNeighbor.metadata.rightEdgeHeight;
            }
            return fallback;
        }
        
        /// <summary>
        /// Holt die linke Kanten-Höhe des rechten Nachbar-Chunks.
        /// </summary>
        public static int GetRightNeighborHeight(GenerationContext context, int fallback = -1)
        {
            if (context.rightNeighbor != null && context.rightNeighbor.metadata.isComplete)
            {
                return context.rightNeighbor.metadata.leftEdgeHeight;
            }
            return fallback;
        }
        
        #endregion
        
        #region Tile Checks
        
        /// <summary>
        /// Prüft ob eine Position leer ist (Air, Gap, oder außerhalb Bounds).
        /// </summary>
        public static bool IsEmpty(ChunkData chunk, int x, int y)
        {
            if (!chunk.IsInBounds(x, y)) return true;
            
            TileData tile = chunk[x, y];
            return tile.IsEmpty || tile.type == TileType.Air || tile.type == TileType.Gap;
        }
        
        /// <summary>
        /// Prüft ob eine Position solid ist (begehbar).
        /// </summary>
        public static bool IsSolid(ChunkData chunk, int x, int y)
        {
            if (!chunk.IsInBounds(x, y)) return false;
            return chunk[x, y].IsSolid;
        }
        
        /// <summary>
        /// Prüft ob eine Position ein Gap ist.
        /// </summary>
        public static bool IsGap(ChunkData chunk, int x, int y)
        {
            if (!chunk.IsInBounds(x, y)) return true;
            return chunk[x, y].type == TileType.Gap;
        }
        
        /// <summary>
        /// Prüft ob genug freier Platz an einer Position ist.
        /// </summary>
        public static bool HasFreeSpace(ChunkData chunk, int x, int y, int left, int right, int up)
        {
            // Position selbst
            if (!IsEmpty(chunk, x, y)) return false;
            
            // Links
            for (int i = 1; i <= left; i++)
            {
                if (!IsEmpty(chunk, x - i, y)) return false;
            }
            
            // Rechts
            for (int i = 1; i <= right; i++)
            {
                if (!IsEmpty(chunk, x + i, y)) return false;
            }
            
            // Oben
            for (int i = 1; i <= up; i++)
            {
                if (!IsEmpty(chunk, x, y + i)) return false;
            }
            
            return true;
        }
        
        #endregion
        
        #region Position Calculation
        
        /// <summary>
        /// Berechnet die World-Position für Entity-Spawning.
        /// Zentriert im Tile direkt über der Oberfläche.
        /// heightAboveSurface ist ein ZUSÄTZLICHER Offset (0 = zentriert im ersten Tile über Ground).
        /// </summary>
        public static Vector3 CalculateSpawnPosition(
            ChunkData chunk, 
            GenerationContext context, 
            int localX, 
            int surfaceY, 
            float heightAboveSurface = 0f,
            bool centerInTile = true)
        {
            int worldX = chunk.LocalToWorldX(localX);
            
            float posX = centerInTile 
                ? (worldX + 0.5f) * context.cellSize.x 
                : worldX * context.cellSize.x;
            
            // surfaceY + 1 = Tile über Ground
            // + 0.5 = Zentrierung in diesem Tile (fest eingebaut)
            // + heightAboveSurface = zusätzlicher Offset
            float posY = (surfaceY + 1.5f + heightAboveSurface) * context.cellSize.y;
            
            return new Vector3(posX, posY, 0);
        }
        
        /// <summary>
        /// Konvertiert Tile-Koordinaten zu World-Position (Tile-Zentrum).
        /// </summary>
        public static Vector3 TileToWorldPosition(int tileX, int tileY, Vector2 cellSize)
        {
            return new Vector3(
                (tileX + 0.5f) * cellSize.x,
                (tileY + 0.5f) * cellSize.y,
                0
            );
        }
        
        #endregion
        
        #region Safe Start
        
        /// <summary>
        /// Prüft ob eine World-X-Position innerhalb der Safe-Start-Zone ist.
        /// </summary>
        public static bool IsInSafeStartZone(int worldX, int safeStartColumns)
        {
            return worldX < safeStartColumns;
        }
        
        #endregion
        
        #region Noise Helpers
        
        /// <summary>
        /// Berechnet eine noise-basierte Höhe zwischen min und max.
        /// </summary>
        public static int GetNoiseHeight(GenerationContext context, float worldX, float frequency, int minHeight, int maxHeight, int octaves = 3, float seedOffset = 0f)
        {
            float noise = context.GetFractalNoise(worldX, frequency, octaves, seedOffset);
            return Mathf.RoundToInt(Mathf.Lerp(minHeight, maxHeight, noise));
        }
        
        #endregion
    }
}
