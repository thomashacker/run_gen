using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Generates floating platforms: one per layer per chunk. Random length and thickness for the whole platform.
    /// Platform top is a smooth height graph from fBm (multi-octave Perlin noise); tiles are filled below it by thickness, then placed above ground/reference.
    /// </summary>
    public class PlatformPass : GeneratorPassBase
    {
        [System.Serializable]
        public class PlatformLayer
        {
            public string name = "Layer";
            public bool enabled = true;

            [Header("Clearance")]
            [Tooltip("Minimum air tiles between reference surface and bottom of platform")]
            public int minHeightAboveGround = 4;

            [Header("Dimensions (one value per platform)")]
            public int minLength = 4;
            public int maxLength = 12;
            public int minThickness = 1;
            public int maxThickness = 2;

            [Header("Noise (fBm for top curve)")]
            [Tooltip("Base frequency for the height graph; higher = more wavy")]
            [Range(0.02f, 0.2f)]
            public float noiseFrequency = 0.08f;
            public float noiseSeedOffset = 0f;
            [Tooltip("How many tiles the top curve can vary (amplitude of the graph)")]
            [Range(0, 4)]
            public int heightVariation = 2;
            [Range(2, 5)]
            [Tooltip("Octaves for fractal noise; more = more natural detail")]
            public int octaves = 3;
            [Tooltip("Persistence (gain) per octave; 0.5 = classic fBm")]
            [Range(0.3f, 0.7f)]
            public float persistence = 0.5f;

            [Header("Spawn")]
            [Range(0f, 1f)]
            public float spawnDensity = 0.6f;
            [Tooltip("No platform in the first N world columns (per layer)")]
            public int safeStartColumns = 20;
        }

        [Header("Platform Layers")]
        public List<PlatformLayer> layers = new List<PlatformLayer>();

        const float Lacunarity = 2f;

        public override void Initialize(GenerationContext context) { }

        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!layer.enabled) continue;
                if (Random.value >= layer.spawnDensity) continue;

                int worldChunkStartX = chunk.chunkIndex * chunk.width;
                if (worldChunkStartX + chunk.width <= layer.safeStartColumns) continue;

                int length = Random.Range(layer.minLength, layer.maxLength + 1);
                int thickness = Random.Range(layer.minThickness, layer.maxThickness + 1);
                if (length > chunk.width) continue;

                int placeX = Random.Range(0, chunk.width - length + 1);
                int worldXBase = chunk.chunkIndex * chunk.width + placeX;
                float[] topGraph = BuildTopGraph(length, thickness, worldXBase, layer, context, i);
                int relMinBottom = 0;
                int relMaxTop = 0;
                for (int c = 0; c < length; c++)
                {
                    int topY = Mathf.RoundToInt(topGraph[c]) - 1;
                    int bottomY = topY - thickness + 1;
                    if (bottomY < relMinBottom) relMinBottom = bottomY;
                    if (topY > relMaxTop) relMaxTop = topY;
                }
                int refSurface = GetMaxReferenceInRange(chunk, placeX, placeX + length - 1, i);
                int placeY = refSurface + 1 + layer.minHeightAboveGround - relMinBottom;

                if (placeY < 0 || placeY + relMaxTop >= chunk.height) continue;

                for (int c = 0; c < length; c++)
                {
                    int topRel = Mathf.RoundToInt(topGraph[c]) - 1;
                    int bottomRel = topRel - thickness + 1;
                    int cx = placeX + c;
                    int bottomY = placeY + bottomRel;
                    int topY = placeY + topRel;
                    for (int cy = bottomY; cy <= topY; cy++)
                    {
                        if (!chunk.IsInBounds(cx, cy)) continue;
                        var tile = TileData.Solid(TileLayer.Platform);
                        tile.type = TileType.Platform;
                        tile.heightLevel = cy;
                        if (cy == topY)
                            tile.flags |= TileFlags.EdgeTop;
                        chunk[cx, cy] = tile;
                    }
                }
            }
            return chunk;
        }

        /// <summary>
        /// Builds the platform top curve using fBm (fractal Brownian motion): sum of Perlin octaves for a natural, terrain-like graph.
        /// worldXBase (chunk index * width + placeX) seeds the noise so each platform gets a different curve.
        /// </summary>
        float[] BuildTopGraph(int length, int thickness, int worldXBase, PlatformLayer layer, GenerationContext context, int layerIndex)
        {
            float[] top = new float[length];
            float seedX = context.globalSeed + layer.noiseSeedOffset + layerIndex * 100f + worldXBase;
            float seedY = context.globalSeed * 0.7f + layerIndex * 50f;

            for (int c = 0; c < length; c++)
            {
                float x = seedX + c * layer.noiseFrequency;
                float sum = 0f;
                float f = 1f;
                float a = 1f;
                float m = 0f;
                for (int o = 0; o < layer.octaves; o++)
                {
                    float nx = x * f + o * 17.3f;
                    float ny = seedY * 0.3f + o * 31.7f;
                    sum += Mathf.PerlinNoise(nx, ny) * a;
                    m += a;
                    f *= Lacunarity;
                    a *= layer.persistence;
                }
                float normalized = sum / m;
                float variation = (normalized - 0.5f) * 2f * Mathf.Max(0, layer.heightVariation);
                top[c] = thickness + variation;
            }
            return top;
        }

        int GetMaxReferenceInRange(ChunkData chunk, int startX, int endX, int layerIndex)
        {
            int maxRef = -1;
            for (int x = startX; x <= endX; x++)
            {
                if (x < 0 || x >= chunk.width) continue;
                int refY = layerIndex > 0 ? GetPlatformTopInColumn(chunk, x) : -1;
                if (refY < 0)
                    refY = GetGroundSurfaceHeight(chunk, x);
                if (refY >= 0)
                    maxRef = maxRef < 0 ? refY : Mathf.Max(maxRef, refY);
            }
            return maxRef >= 0 ? maxRef : 0;
        }

        int GetGroundSurfaceHeight(ChunkData chunk, int localX)
        {
            if (localX < 0 || localX >= chunk.width) return -1;
            for (int y = chunk.height - 1; y >= 0; y--)
            {
                var tile = chunk[localX, y];
                if (tile.layer == TileLayer.Ground && tile.IsWalkable)
                    return y;
            }
            return -1;
        }

        int GetPlatformTopInColumn(ChunkData chunk, int localX)
        {
            if (localX < 0 || localX >= chunk.width) return -1;
            for (int y = chunk.height - 1; y >= 0; y--)
            {
                var tile = chunk[localX, y];
                if (tile.layer == TileLayer.Platform && tile.IsSolid)
                    return y;
            }
            return -1;
        }

        void Reset()
        {
            passName = "Platform Pass";
            if (layers.Count == 0)
            {
                layers.Add(new PlatformLayer
                {
                    name = "Lower Platforms",
                    minHeightAboveGround = 3,
                    spawnDensity = 0.5f
                });
                layers.Add(new PlatformLayer
                {
                    name = "Upper Platforms",
                    minHeightAboveGround = 6,
                    noiseSeedOffset = 50f,
                    spawnDensity = 0.3f,
                    minLength = 3,
                    maxLength = 8
                });
            }
        }
    }
}
