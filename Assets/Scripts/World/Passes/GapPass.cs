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
        /// Scans the actual tile matrix for neighbor heights instead of
        /// relying on metadata, so it works regardless of earlier passes.
        /// </summary>
        void FillGapBackground(ChunkData chunk, GapSpan span)
        {
            // Get neighbor surface heights by scanning the real tile matrix
            // (not metadata, which may be stale within the same pass).
            int leftNeighborX = span.startX - 1;
            int rightNeighborX = span.endX + 1;
            
            int leftHeight = leftNeighborX >= 0
                ? chunk.GetSurfaceHeight(leftNeighborX)
                : -1;
            int rightHeight = rightNeighborX < chunk.width
                ? chunk.GetSurfaceHeight(rightNeighborX)
                : -1;
            
            // If one side is out of bounds or has no ground, use the other side.
            // If both are missing, skip background entirely.
            int minHeight;
            if (leftHeight >= 0 && rightHeight >= 0)
                minHeight = Mathf.Min(leftHeight, rightHeight);
            else if (leftHeight >= 0)
                minHeight = leftHeight;
            else if (rightHeight >= 0)
                minHeight = rightHeight;
            else
                return;
            
            // Cap = min neighbor height + offset
            int bgCap = minHeight + backgroundYOffset;
            if (bgCap < gapBottomOffset) return;
            
            var bgData = new TileData
            {
                type = TileType.Solid,
                layer = TileLayer.Background,
                backgroundType = backgroundType
            };
            
            // Gap columns are already cleared — just fill from bottom to cap.
            int bottomY = gapBottomOffset;
            for (int x = span.startX; x <= span.endX; x++)
            {
                for (int y = bottomY; y <= bgCap; y++)
                {
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
