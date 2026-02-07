using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Generiert den Hauptboden basierend auf Perlin Noise.
    /// Setzt die Grundform des Terrains (ohne Gaps, ohne Rampen).
    /// </summary>
    public class GroundPass : GeneratorPassBase
    {
        [Header("Height Settings")]
        public int baseHeight = 3;
        public int minHeight = 2;
        public int maxHeight = 10;
        
        [Header("Noise Settings")]
        [Range(0.01f, 0.2f)]
        public float noiseFrequency = 0.05f;
        [Range(1, 4)]
        public int noiseOctaves = 3;
        public float noiseSeedOffset = 0f;
        
        [Header("Terrain Smoothing")]
        [Range(1, 3)]
        [Tooltip("Maximale Höhenänderung pro Spalte (1 = smooth)")]
        public int maxSlopePerColumn = 1;
        
        [Header("Plateaus")]
        [Range(0f, 0.5f)]
        public float plateauChance = 0.3f;
        public int minPlateauLength = 3;
        public int maxPlateauLength = 10;
        
        // State (wird pro Chunk zurückgesetzt wenn nötig)
        private int lastHeight;
        private int plateauRemaining;
        
        public override void Initialize(GenerationContext context)
        {
            lastHeight = baseHeight;
            plateauRemaining = 0;
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            // State vom linken Nachbar übernehmen für seamless
            if (context.leftNeighbor != null && context.leftNeighbor.metadata.isComplete)
            {
                lastHeight = context.leftNeighbor.metadata.rightEdgeHeight;
            }
            
            // Höhenkarte für diesen Chunk initialisieren
            chunk.metadata.surfaceHeights = new int[chunk.width];
            
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                int height = CalculateHeight(worldX, context);
                
                // Höhe speichern
                chunk.metadata.surfaceHeights[x] = height;
                
                // Spalte mit Solid füllen
                for (int y = 0; y < height; y++)
                {
                    var tile = TileData.Solid(TileLayer.Ground);
                    tile.heightLevel = y;
                    
                    // Edge-Flag für oberste Tiles
                    if (y == height - 1)
                        tile.flags |= TileFlags.EdgeTop;
                    
                    chunk[x, y] = tile;
                }
                
                // Letzte Höhe merken
                lastHeight = height;
            }
            
            // Randwerte für Nachbar-Chunks speichern
            chunk.metadata.leftEdgeHeight = chunk.metadata.surfaceHeights[0];
            chunk.metadata.rightEdgeHeight = chunk.metadata.surfaceHeights[chunk.width - 1];
            
            return chunk;
        }
        
        int CalculateHeight(int worldX, GenerationContext context)
        {
            // Plateau aktiv?
            if (plateauRemaining > 0)
            {
                plateauRemaining--;
                return lastHeight;
            }
            
            // Noise-basierte Höhe
            float noise = context.GetFractalNoise(worldX, noiseFrequency, noiseOctaves, noiseSeedOffset);
            int targetHeight = Mathf.RoundToInt(Mathf.Lerp(minHeight, maxHeight, noise));
            
            // Slope begrenzen
            targetHeight = Mathf.Clamp(targetHeight, 
                lastHeight - maxSlopePerColumn, 
                lastHeight + maxSlopePerColumn);
            
            // Clamp auf min/max
            targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);
            
            // Plateau starten?
            if (Random.value < plateauChance)
            {
                plateauRemaining = Random.Range(minPlateauLength, maxPlateauLength + 1);
            }
            
            return targetHeight;
        }
        
        void Reset()
        {
            passName = "Ground Pass";
        }
    }
}
