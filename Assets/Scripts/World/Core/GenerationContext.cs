using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Kontext-Objekt das an alle Passes übergeben wird.
    /// Enthält globale Einstellungen und Zugriff auf Nachbar-Chunks.
    /// </summary>
    public class GenerationContext
    {
        // === GLOBAL SETTINGS ===
        
        /// <summary>
        /// Globaler Seed für deterministische Generation.
        /// </summary>
        public float globalSeed;
        
        /// <summary>
        /// Chunk-Breite in Tiles.
        /// </summary>
        public int chunkWidth;
        
        /// <summary>
        /// Chunk-Höhe in Tiles.
        /// </summary>
        public int chunkHeight;
        
        /// <summary>
        /// Grid Cell-Größe (für World-Koordinaten).
        /// </summary>
        public Vector2 cellSize;
        
        // === NEIGHBOR ACCESS ===
        
        /// <summary>
        /// Referenz auf den linken Nachbar-Chunk (kann null sein).
        /// </summary>
        public ChunkData leftNeighbor;
        
        /// <summary>
        /// Referenz auf den rechten Nachbar-Chunk (kann null sein).
        /// </summary>
        public ChunkData rightNeighbor;
        
        // === PASS COMMUNICATION ===
        
        /// <summary>
        /// Shared Data zwischen Passes.
        /// Passes können hier Daten für nachfolgende Passes speichern.
        /// </summary>
        private Dictionary<string, object> sharedData = new Dictionary<string, object>();
        
        /// <summary>
        /// Speichert Daten für nachfolgende Passes.
        /// </summary>
        public void SetData<T>(string key, T value)
        {
            sharedData[key] = value;
        }
        
        /// <summary>
        /// Holt Daten die ein vorheriger Pass gespeichert hat.
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (sharedData.TryGetValue(key, out object value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }
        
        /// <summary>
        /// Prüft ob Daten existieren.
        /// </summary>
        public bool HasData(string key)
        {
            return sharedData.ContainsKey(key);
        }
        
        // === HELPER METHODS ===
        
        /// <summary>
        /// Konvertiert World-X zu Chunk-Index.
        /// </summary>
        public int WorldToChunkIndex(float worldX)
        {
            return Mathf.FloorToInt(worldX / (chunkWidth * cellSize.x));
        }
        
        /// <summary>
        /// Konvertiert World-X zu lokaler X-Koordinate im Chunk.
        /// </summary>
        public int WorldToLocalX(float worldX, int chunkIndex)
        {
            int worldTileX = Mathf.FloorToInt(worldX / cellSize.x);
            return worldTileX - (chunkIndex * chunkWidth);
        }
        
        /// <summary>
        /// Generiert einen Noise-Wert basierend auf Position und Seed.
        /// </summary>
        public float GetNoise(float x, float frequency, float seedOffset = 0f)
        {
            return Mathf.PerlinNoise((x + globalSeed + seedOffset) * frequency, globalSeed * 0.5f);
        }
        
        /// <summary>
        /// Generiert Fractal Noise (mehrere Oktaven).
        /// </summary>
        public float GetFractalNoise(float x, float frequency, int octaves = 3, float seedOffset = 0f)
        {
            float n = 0f;
            float freq = frequency;
            float amp = 1f;
            float maxAmp = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                n += Mathf.PerlinNoise((x + globalSeed + seedOffset) * freq, (globalSeed + seedOffset) * 0.3f) * amp;
                maxAmp += amp;
                freq *= 2f;
                amp *= 0.5f;
            }
            
            return n / maxAmp;
        }
        
        /// <summary>
        /// 2D Fractal Noise (fBm) für Höhlen/Caves – (x,y) → [0,1]. Gleiche Logik wie GetFractalNoise, mit Y.
        /// </summary>
        public float GetFractalNoise2D(float x, float y, float frequency, int octaves = 3, float seedOffset = 0f)
        {
            float n = 0f;
            float freq = frequency;
            float amp = 1f;
            float maxAmp = 0f;
            float seedX = globalSeed + seedOffset;
            float seedY = globalSeed * 0.7f + seedOffset * 1.3f;
            for (int i = 0; i < octaves; i++)
            {
                float nx = x * freq + seedX + i * 17.3f;
                float ny = y * freq + seedY + i * 31.7f;
                n += Mathf.PerlinNoise(nx, ny) * amp;
                maxAmp += amp;
                freq *= 2f;
                amp *= 0.5f;
            }
            return n / maxAmp;
        }
        
        /// <summary>
        /// Holt die Oberflächen-Höhe an einer World-X Position.
        /// Berücksichtigt den aktuellen Chunk und Nachbarn.
        /// </summary>
        public int GetSurfaceHeightAt(int worldX, ChunkData currentChunk)
        {
            int localX = worldX - (currentChunk.chunkIndex * chunkWidth);
            
            // Im aktuellen Chunk?
            if (localX >= 0 && localX < chunkWidth)
            {
                if (currentChunk.metadata.surfaceHeights != null && 
                    localX < currentChunk.metadata.surfaceHeights.Length)
                {
                    return currentChunk.metadata.surfaceHeights[localX];
                }
                return currentChunk.GetSurfaceHeight(localX);
            }
            
            // Im linken Nachbar?
            if (localX < 0 && leftNeighbor != null)
            {
                int neighborLocalX = localX + chunkWidth;
                if (leftNeighbor.metadata.surfaceHeights != null &&
                    neighborLocalX >= 0 && neighborLocalX < leftNeighbor.metadata.surfaceHeights.Length)
                {
                    return leftNeighbor.metadata.surfaceHeights[neighborLocalX];
                }
            }
            
            // Im rechten Nachbar?
            if (localX >= chunkWidth && rightNeighbor != null)
            {
                int neighborLocalX = localX - chunkWidth;
                if (rightNeighbor.metadata.surfaceHeights != null &&
                    neighborLocalX >= 0 && neighborLocalX < rightNeighbor.metadata.surfaceHeights.Length)
                {
                    return rightNeighbor.metadata.surfaceHeights[neighborLocalX];
                }
            }
            
            return -1;
        }
    }
}
