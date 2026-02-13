using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Schneidet Löcher/Gaps in den Boden.
    /// Arbeitet auf der Matrix die vom GroundPass erstellt wurde.
    /// Two-pass approach:
    ///   1) Cut all gaps and record their column spans.
    ///   2) Place background tiles capped by the min neighbor height so they never stick out above adjacent ground.
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
        
        [Header("Background")]
        [Tooltip("Background type to place where blocks are removed")]
        public BackgroundType backgroundType = BackgroundType.Default;
        [Tooltip("Y offset applied to the neighbor-based background cap (e.g. -1 = one tile below neighbor surface)")]
        public int backgroundYOffset = -1;
        
        // State
        private int totalColumns;
        private int gapRemaining;
        private int cooldownRemaining;
        
        /// <summary>
        /// Records a contiguous gap span (start and end column, inclusive).
        /// </summary>
        struct GapSpan
        {
            public int startX;
            public int endX;
        }
        
        public override void Initialize(GenerationContext context)
        {
            totalColumns = 0;
            gapRemaining = 0;
            cooldownRemaining = 0;
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            var gapSpans = new List<GapSpan>();
            int currentGapStart = -1;
            
            // --- Pass 1: Cut gaps and record spans ---
            for (int x = 0; x < chunk.width; x++)
            {
                totalColumns++;
                
                if (cooldownRemaining > 0)
                    cooldownRemaining--;
                
                if (gapRemaining > 0)
                {
                    CutGapColumn(chunk, x);
                    gapRemaining--;
                    
                    // End of gap?
                    if (gapRemaining == 0)
                    {
                        gapSpans.Add(new GapSpan { startX = currentGapStart, endX = x });
                        currentGapStart = -1;
                    }
                    continue;
                }
                
                if (CanStartGap())
                {
                    gapRemaining = Random.Range(minGapWidth, maxGapWidth + 1);
                    cooldownRemaining = minCooldown;
                    currentGapStart = x;
                    
                    CutGapColumn(chunk, x);
                    gapRemaining--;
                    
                    if (gapRemaining == 0)
                    {
                        gapSpans.Add(new GapSpan { startX = currentGapStart, endX = x });
                        currentGapStart = -1;
                    }
                }
            }
            
            // Gap that extends to chunk edge
            if (currentGapStart >= 0)
                gapSpans.Add(new GapSpan { startX = currentGapStart, endX = chunk.width - 1 });
            
            // --- Pass 2: Place background tiles capped by neighbor height ---
            foreach (var span in gapSpans)
                FillGapBackground(chunk, span);
            
            return chunk;
        }
        
        /// <summary>
        /// Pass 1: Removes foreground tiles in a gap column.
        /// </summary>
        void CutGapColumn(ChunkData chunk, int x)
        {
            int surfaceHeight = ChunkUtilities.GetGroundHeight(chunk, x, -1);
            if (surfaceHeight < 0) return;
            
            int bottomY = gapBottomOffset;
            for (int y = bottomY; y <= surfaceHeight; y++)
            {
                chunk[x, y] = TileData.Gap;
            }
        }
        
        /// <summary>
        /// Pass 2: Places background tiles in a gap span, capped by the
        /// min surface height of the left and right neighbors so the
        /// background never sticks out above the adjacent ground.
        /// </summary>
        void FillGapBackground(ChunkData chunk, GapSpan span)
        {
            // Get neighbor surface heights (columns just outside the gap)
            int leftNeighborX = span.startX - 1;
            int rightNeighborX = span.endX + 1;
            
            int leftHeight = leftNeighborX >= 0
                ? ChunkUtilities.GetGroundHeight(chunk, leftNeighborX, -1)
                : int.MaxValue;
            int rightHeight = rightNeighborX < chunk.width
                ? ChunkUtilities.GetGroundHeight(chunk, rightNeighborX, -1)
                : int.MaxValue;
            
            // Cap = min of both neighbors + offset
            int bgCap = Mathf.Min(leftHeight, rightHeight) + backgroundYOffset;
            
            var bgData = new TileData
            {
                type = TileType.Solid,
                layer = TileLayer.Background,
                backgroundType = backgroundType
            };
            
            int bottomY = gapBottomOffset;
            for (int x = span.startX; x <= span.endX; x++)
            {
                int surfaceHeight = ChunkUtilities.GetGroundHeight(chunk, x, -1);
                int topY = surfaceHeight >= 0 ? surfaceHeight : 0;
                
                for (int y = bottomY; y <= topY; y++)
                {
                    if (y <= bgCap)
                        chunk.SetBackgroundTile(x, y, bgData);
                }
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
