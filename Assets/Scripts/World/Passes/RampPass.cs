using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Fügt Rampen bei Höhenunterschieden im Ground hinzu.
    /// </summary>
    public class RampPass : GeneratorPassBase
    {
        [Header("Ramp Chance")]
        [Range(0f, 1f)]
        public float rampChance = 0.7f;

        [Header("Detection")]
        public bool onlyForSingleSteps = true;
        public int maxHeightDiff = 2;

        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            if (chunk.metadata.surfaceHeights == null)
                return chunk;

            ProcessGroundRamps(chunk, context);
            return chunk;
        }

        void ProcessGroundRamps(ChunkData chunk, GenerationContext context)
        {
            int[] heights = chunk.metadata.surfaceHeights;

            for (int x = 0; x < chunk.width; x++)
            {
                int curr = heights[x];
                if (curr < 0) continue;

                int prev = (x > 0) ? heights[x - 1] : GetLeftNeighborHeight(context);
                if (prev < 0) continue;

                int diff = curr - prev;
                if (!IsValidHeightDiff(diff)) continue;
                if (Random.value > rampChance) continue;

                // Gap check
                if (IsGapAt(chunk, x, Mathf.Min(curr, prev) - 1)) continue;

                // Rampe wird ÜBER dem niedrigeren Block HINZUGEFÜGT (nicht ersetzt)
                // Aufwärts: prev ist niedriger → Rampe bei x-1, Y = prev (auf dem niedrigeren Block)
                // Abwärts: curr ist niedriger → Rampe bei x, Y = curr (auf dem niedrigeren Block)
                
                if (diff > 0)
                {
                    // AUFWÄRTS: Ground steigt von links nach rechts
                    // UpRamp (/) kommt auf den NIEDRIGEREN Block (links, bei x-1)
                    int rampX = x - 1;
                    int rampY = prev; // Höhe des niedrigeren Blocks
                    if (rampX >= 0)
                        TryPlaceRamp(chunk, rampX, rampY, true);
                }
                else
                {
                    // ABWÄRTS: Ground fällt von links nach rechts  
                    // DownRamp (\) kommt auf den NIEDRIGEREN Block (rechts, bei x)
                    int rampX = x;
                    int rampY = curr; // Höhe des niedrigeren Blocks
                    TryPlaceRamp(chunk, rampX, rampY, false);
                }
            }
        }

        int GetLeftNeighborHeight(GenerationContext context)
        {
            if (context.leftNeighbor != null && context.leftNeighbor.metadata.isComplete)
                return context.leftNeighbor.metadata.rightEdgeHeight;
            return -1;
        }

        void TryPlaceRamp(ChunkData chunk, int x, int y, bool isUpRamp)
        {
            if (!chunk.IsInBounds(x, y)) return;

            // Position muss leer sein
            TileData existing = chunk[x, y];
            if (!existing.IsEmpty && existing.type != TileType.Air) return;

            // REGEL 1: Mindestens ein horizontaler Nachbar frei
            if (!HasFreeNeighbor(chunk, x, y)) return;

            // REGEL 2: Keine gegenläufige Rampe daneben (keine Spitzen)
            if (HasOpposingRampNeighbor(chunk, x, y, isUpRamp)) return;

            // Rampe setzen
            TileData ramp = TileData.Ramp(isUpRamp, TileLayer.Ground);
            ramp.heightLevel = y;
            ramp.flags |= TileFlags.EdgeTop;
            chunk[x, y] = ramp;
        }

        bool HasFreeNeighbor(ChunkData chunk, int x, int y)
        {
            if (x <= 0 || x >= chunk.width - 1) return true;

            TileData left = chunk[x - 1, y];
            if (left.IsEmpty || left.type == TileType.Air) return true;

            TileData right = chunk[x + 1, y];
            if (right.IsEmpty || right.type == TileType.Air) return true;

            return false;
        }

        bool HasOpposingRampNeighbor(ChunkData chunk, int x, int y, bool isUpRamp)
        {
            if (x > 0)
            {
                TileData left = chunk[x - 1, y];
                if (left.IsRamp)
                {
                    bool leftIsUp = left.type == TileType.RampUp;
                    if (isUpRamp && !leftIsUp) return true;
                    if (!isUpRamp && leftIsUp) return true;
                }
            }

            if (x < chunk.width - 1)
            {
                TileData right = chunk[x + 1, y];
                if (right.IsRamp)
                {
                    bool rightIsUp = right.type == TileType.RampUp;
                    if (isUpRamp && !rightIsUp) return true;
                    if (!isUpRamp && rightIsUp) return true;
                }
            }

            return false;
        }

        bool IsValidHeightDiff(int heightDiff)
        {
            if (heightDiff == 0) return false;
            if (onlyForSingleSteps) return Mathf.Abs(heightDiff) == 1;
            return Mathf.Abs(heightDiff) <= maxHeightDiff;
        }

        bool IsGapAt(ChunkData chunk, int x, int y)
        {
            if (!chunk.IsInBounds(x, y)) return true;
            TileData tile = chunk[x, y];
            return tile.type == TileType.Gap || tile.type == TileType.Air;
        }

        void Reset() => passName = "Ramp Pass";
    }
}
