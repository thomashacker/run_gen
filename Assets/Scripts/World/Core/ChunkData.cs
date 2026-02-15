using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Repräsentiert die Daten eines einzelnen Chunks.
    /// Enthält eine 2D-Matrix von TileData.
    /// </summary>
    [System.Serializable]
    public class ChunkData
    {
        /// <summary>
        /// Chunk-Index (X-Position in Chunk-Koordinaten).
        /// </summary>
        public int chunkIndex;
        
        /// <summary>
        /// Breite des Chunks in Tiles.
        /// </summary>
        public int width;
        
        /// <summary>
        /// Höhe des Chunks in Tiles.
        /// </summary>
        public int height;
        
        /// <summary>
        /// Die Tile-Matrix [x, y] (Ground, Platform, Entities, etc.).
        /// </summary>
        private TileData[,] tiles;
        
        /// <summary>
        /// Hintergrund-Tile-Matrix [x, y]. Rein dekorativ, kein Collider, wird hinter allem gerendert.
        /// </summary>
        private TileData[,] backgroundTiles;
        
        /// <summary>
        /// Zusätzliche Metadaten die Passes speichern können.
        /// </summary>
        public ChunkMetadata metadata;
        
        // === CONSTRUCTOR ===
        
        public ChunkData(int chunkIndex, int width, int height)
        {
            this.chunkIndex = chunkIndex;
            this.width = width;
            this.height = height;
            this.tiles = new TileData[width, height];
            this.backgroundTiles = new TileData[width, height];
            this.metadata = new ChunkMetadata();
            
            // Mit Air initialisieren
            Clear();
        }
        
        // === ACCESSORS ===
        
        /// <summary>
        /// Zugriff auf ein Tile. Gibt Air zurück wenn außerhalb der Bounds.
        /// </summary>
        public TileData this[int x, int y]
        {
            get
            {
                if (!IsInBounds(x, y))
                    return TileData.Air;
                return tiles[x, y];
            }
            set
            {
                if (IsInBounds(x, y))
                    tiles[x, y] = value;
            }
        }
        
        /// <summary>
        /// Zugriff mit Vector2Int.
        /// </summary>
        public TileData this[Vector2Int pos]
        {
            get => this[pos.x, pos.y];
            set => this[pos.x, pos.y] = value;
        }
        
        /// <summary>
        /// Holt das Hintergrund-Tile an (x, y). Air wenn außerhalb oder leer.
        /// </summary>
        public TileData GetBackgroundTile(int x, int y)
        {
            if (!IsInBounds(x, y))
                return TileData.Air;
            return backgroundTiles[x, y];
        }
        
        /// <summary>
        /// Setzt das Hintergrund-Tile an (x, y).
        /// </summary>
        public void SetBackgroundTile(int x, int y, TileData data)
        {
            if (IsInBounds(x, y))
                backgroundTiles[x, y] = data;
        }
        
        /// <summary>
        /// Füllt eine Spalte im Hintergrund von bottom bis top (inklusive).
        /// </summary>
        public void FillBackgroundColumn(int x, int bottom, int top, TileData data)
        {
            for (int y = bottom; y <= top && y < height; y++)
            {
                if (y >= 0)
                    SetBackgroundTile(x, y, data);
            }
        }
        
        // === METHODS ===
        
        /// <summary>
        /// Prüft ob Koordinaten innerhalb der Matrix liegen.
        /// </summary>
        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }
        
        /// <summary>
        /// Setzt alle Tiles auf Air.
        /// </summary>
        public void Clear()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tiles[x, y] = TileData.Air;
                    backgroundTiles[x, y] = TileData.Air;
                }
            }
            metadata = new ChunkMetadata();
        }
        
        /// <summary>
        /// Rebuilds metadata.surfaceHeights from the actual tile matrix.
        /// Call after any pass that modifies ground tiles so that subsequent
        /// passes (and utility methods like GetGroundHeight) see accurate data.
        /// Also updates leftEdgeHeight / rightEdgeHeight.
        /// </summary>
        public void RefreshSurfaceHeights()
        {
            if (metadata.surfaceHeights == null || metadata.surfaceHeights.Length != width)
                metadata.surfaceHeights = new int[width];
            
            for (int x = 0; x < width; x++)
            {
                metadata.surfaceHeights[x] = GetSurfaceHeight(x);
            }
            
            // Keep edge heights in sync
            if (width > 0)
            {
                metadata.leftEdgeHeight = metadata.surfaceHeights[0];
                metadata.rightEdgeHeight = metadata.surfaceHeights[width - 1];
            }
        }
        
        /// <summary>
        /// Konvertiert lokale Chunk-Koordinaten zu World-X.
        /// </summary>
        public int LocalToWorldX(int localX)
        {
            return chunkIndex * width + localX;
        }
        
        /// <summary>
        /// Konvertiert World-X zu lokalen Chunk-Koordinaten.
        /// </summary>
        public int WorldToLocalX(int worldX)
        {
            return worldX - (chunkIndex * width);
        }
        
        /// <summary>
        /// Gibt die erste Y-Position zurück die Solid ist (von oben nach unten).
        /// Returns -1 wenn keine gefunden.
        /// </summary>
        public int GetSurfaceHeight(int localX)
        {
            if (localX < 0 || localX >= width)
                return -1;
            
            for (int y = height - 1; y >= 0; y--)
            {
                if (tiles[localX, y].IsWalkable)
                    return y;
            }
            return -1;
        }
        
        /// <summary>
        /// Highest Y in this column that has a solid Ground or Platform tile (for background fill).
        /// Explicitly includes both layers so background is drawn under ground and under platforms.
        /// </summary>
        public int GetHighestSolidHeight(int localX)
        {
            if (localX < 0 || localX >= width)
                return -1;
            for (int y = height - 1; y >= 0; y--)
            {
                var t = tiles[localX, y];
                if (t.IsEmpty) continue;
                if ((t.layer == TileLayer.Ground || t.layer == TileLayer.Platform) && t.IsWalkable)
                    return y;
            }
            return -1;
        }
        
        /// <summary>
        /// Füllt eine Spalte von bottom bis top (inklusive).
        /// </summary>
        public void FillColumn(int x, int bottom, int top, TileData data)
        {
            for (int y = bottom; y <= top && y < height; y++)
            {
                if (y >= 0)
                    tiles[x, y] = data;
            }
        }
        
        /// <summary>
        /// Füllt einen rechteckigen Bereich.
        /// </summary>
        public void FillRect(int startX, int startY, int endX, int endY, TileData data)
        {
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    if (IsInBounds(x, y))
                        tiles[x, y] = data;
                }
            }
        }
        
        /// <summary>
        /// Kopiert die Matrix (für Undo/Preview etc.).
        /// </summary>
        public ChunkData Clone()
        {
            ChunkData copy = new ChunkData(chunkIndex, width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    copy.tiles[x, y] = tiles[x, y];
                    copy.backgroundTiles[x, y] = backgroundTiles[x, y];
                }
            }
            copy.metadata = metadata.Clone();
            return copy;
        }
    }
    
    /// <summary>
    /// Zusätzliche Metadaten für einen Chunk.
    /// Passes können hier Informationen für nachfolgende Passes speichern.
    /// </summary>
    [System.Serializable]
    public class ChunkMetadata
    {
        /// <summary>
        /// Höhenkarte der Oberfläche (wird vom GroundPass gesetzt).
        /// Index = lokale X-Koordinate, Wert = Y-Höhe der Oberfläche.
        /// </summary>
        public int[] surfaceHeights;
        
        /// <summary>
        /// Bodenhöhe am linken Rand (für seamless Übergänge).
        /// </summary>
        public int leftEdgeHeight;
        
        /// <summary>
        /// Bodenhöhe am rechten Rand (für seamless Übergänge).
        /// </summary>
        public int rightEdgeHeight;
        
        /// <summary>
        /// Wurde dieser Chunk vollständig generiert?
        /// </summary>
        public bool isComplete;
        
        public ChunkMetadata Clone()
        {
            return new ChunkMetadata
            {
                surfaceHeights = surfaceHeights != null ? (int[])surfaceHeights.Clone() : null,
                leftEdgeHeight = leftEdgeHeight,
                rightEdgeHeight = rightEdgeHeight,
                isComplete = isComplete
            };
        }
    }
}
