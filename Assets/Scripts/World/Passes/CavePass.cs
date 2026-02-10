using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Carves caves into ground using 2D fractal Perlin noise. Runs after GroundPass.
    /// Only removes tiles below the surface; surface and a minimum floor thickness are preserved.
    /// Flood-fill removes dead ends: only cave cells connected to the surface or chunk edges stay.
    /// </summary>
    public class CavePass : GeneratorPassBase
    {
        [Header("Cave Noise (2D fBm)")]
        [Tooltip("Base frequency; lower = larger cave blobs")]
        [Range(0.02f, 0.15f)]
        public float noiseFrequency = 0.04f;
        [Range(1, 4)]
        public int noiseOctaves = 3;
        public float noiseSeedOffset = 100f;
        [Tooltip("Noise below this value = cave (carve). 0.35–0.5 = natural mix")]
        [Range(0.2f, 0.6f)]
        public float caveThreshold = 0.42f;

        [Header("Safety")]
        [Tooltip("Tiles to leave intact below the surface (prevents collapsing floor)")]
        [Range(0, 3)]
        public int minFloorThickness = 1;
        [Tooltip("Bottom rows that are never carved – prevents player falling through the world")]
        [Range(1, 8)]
        public int minBottomRows = 2;
        [Tooltip("World columns at the start where no caves are carved")]
        public int safeStartColumns = 20;
        [Tooltip("Remove cave pockets not connected to surface or chunk edges (no dead ends)")]
        public bool removeDeadEnds = true;

        public override void Initialize(GenerationContext context) { }

        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            int worldChunkStartX = chunk.chunkIndex * chunk.width;
            if (worldChunkStartX + chunk.width <= safeStartColumns)
                return chunk;

            int carveBottom = minBottomRows;

            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                if (worldX < safeStartColumns) continue;

                int surfaceY = chunk.metadata.surfaceHeights != null && x < chunk.metadata.surfaceHeights.Length
                    ? chunk.metadata.surfaceHeights[x]
                    : chunk.GetSurfaceHeight(x);
                if (surfaceY < 0) continue;

                int carveTop = surfaceY - minFloorThickness;
                for (int y = carveBottom; y <= carveTop && y < chunk.height; y++)
                {
                    var tile = chunk[x, y];
                    if (!tile.IsSolid || tile.layer != TileLayer.Ground) continue;

                    float noise = context.GetFractalNoise2D(worldX, y, noiseFrequency, noiseOctaves, noiseSeedOffset);
                    if (noise < caveThreshold)
                        chunk[x, y] = TileData.Air;
                }
            }

            if (removeDeadEnds)
                FillDisconnectedCaves(chunk, carveBottom);

            return chunk;
        }

        /// <summary>
        /// Flood-fill from surface openings and chunk edges; fill back any Air not reached (dead ends).
        /// </summary>
        void FillDisconnectedCaves(ChunkData chunk, int carveBottom)
        {
            var reachable = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            int w = chunk.width;
            int h = chunk.height;

            for (int x = 0; x < w; x++)
            {
                int surfaceY = chunk.metadata.surfaceHeights != null && x < chunk.metadata.surfaceHeights.Length
                    ? chunk.metadata.surfaceHeights[x]
                    : chunk.GetSurfaceHeight(x);
                if (surfaceY < 0) continue;
                int entranceY = surfaceY - minFloorThickness;
                if (entranceY >= carveBottom && entranceY < h && chunk[x, entranceY].IsEmpty)
                {
                    var p = new Vector2Int(x, entranceY);
                    if (reachable.Add(p)) queue.Enqueue(p);
                }
            }
            for (int y = carveBottom; y < h; y++)
            {
                if (chunk[0, y].IsEmpty) { var p = new Vector2Int(0, y); if (reachable.Add(p)) queue.Enqueue(p); }
                if (w > 1 && chunk[w - 1, y].IsEmpty) { var p = new Vector2Int(w - 1, y); if (reachable.Add(p)) queue.Enqueue(p); }
            }

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in new[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
                {
                    int nx = c.x + d.x, ny = c.y + d.y;
                    if (nx < 0 || nx >= w || ny < carveBottom || ny >= h) continue;
                    if (!chunk[nx, ny].IsEmpty) continue;
                    var np = new Vector2Int(nx, ny);
                    if (reachable.Add(np)) queue.Enqueue(np);
                }
            }

            for (int x = 0; x < w; x++)
            {
                int surfaceY = chunk.metadata.surfaceHeights != null && x < chunk.metadata.surfaceHeights.Length
                    ? chunk.metadata.surfaceHeights[x]
                    : chunk.GetSurfaceHeight(x);
                if (surfaceY < 0) continue;
                int carveTop = surfaceY - minFloorThickness;
                for (int y = carveBottom; y <= carveTop && y < h; y++)
                {
                    if (chunk[x, y].IsEmpty && !reachable.Contains(new Vector2Int(x, y)))
                        chunk[x, y] = TileData.Solid(TileLayer.Ground);
                }
            }
        }

        void Reset()
        {
            passName = "Cave Pass";
        }
    }
}
