using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Schneidet Löcher/Gaps in den Boden.
    /// Arbeitet auf der Matrix die vom GroundPass erstellt wurde.
    /// </summary>
    public class GapPass : GeneratorPassBase
    {
        [Header("Gap Settings")]
        [Range(0f, 0.2f)]
        public float gapChance = 0.08f;
        public int minGapWidth = 2;
        public int maxGapWidth = 4;
        
        [Header("Cooldown")]
        [Tooltip("Minimaler Abstand zwischen Gaps (in Spalten)")]
        public int minCooldown = 10;
        
        [Header("Safety")]
        [Tooltip("Spalten am Anfang ohne Gaps")]
        public int safeStartColumns = 15;
        
        [Header("Depth")]
        [Tooltip("Wie tief Gaps gehen (0 = bis ganz unten)")]
        public int gapBottomOffset = 0;
        
        // State
        private int totalColumns;
        private int gapRemaining;
        private int cooldownRemaining;
        
        public override void Initialize(GenerationContext context)
        {
            totalColumns = 0;
            gapRemaining = 0;
            cooldownRemaining = 0;
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            for (int x = 0; x < chunk.width; x++)
            {
                totalColumns++;
                
                // Cooldown reduzieren
                if (cooldownRemaining > 0)
                    cooldownRemaining--;
                
                // Im Gap?
                if (gapRemaining > 0)
                {
                    CutGap(chunk, x);
                    gapRemaining--;
                    continue;
                }
                
                // Neuen Gap starten?
                if (CanStartGap())
                {
                    gapRemaining = Random.Range(minGapWidth, maxGapWidth + 1);
                    cooldownRemaining = minCooldown;
                    
                    CutGap(chunk, x);
                    gapRemaining--;
                }
            }
            
            return chunk;
        }
        
        void CutGap(ChunkData chunk, int x)
        {
            // Oberflächen-Höhe holen
            int surfaceHeight = chunk.metadata.surfaceHeights != null && x < chunk.metadata.surfaceHeights.Length
                ? chunk.metadata.surfaceHeights[x]
                : chunk.GetSurfaceHeight(x);
            
            if (surfaceHeight < 0) return;
            
            // Gap schneiden
            int bottomY = gapBottomOffset;
            for (int y = bottomY; y <= surfaceHeight; y++)
            {
                chunk[x, y] = TileData.Gap;
            }
        }
        
        bool CanStartGap()
        {
            if (totalColumns <= safeStartColumns) return false;
            if (cooldownRemaining > 0) return false;
            return Random.value < gapChance;
        }
        
        void Reset()
        {
            passName = "Gap Pass";
        }
    }
}
