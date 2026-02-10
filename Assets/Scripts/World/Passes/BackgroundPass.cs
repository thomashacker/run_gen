using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Fills the background tile matrix: for each column, finds the highest tile (ground OR platform),
    /// then fills from y=0 up to that height - 1 so the top edge stays foreground-only.
    /// Must run after Ground and Platform passes so platforms are in the chunk when we compute the ceiling.
    /// </summary>
    public class BackgroundPass : GeneratorPassBase
    {
        [Header("Background Type")]
        [Tooltip("Type to write into the background data. ChunkRenderer maps this to the actual tile or spawn.")]
        public BackgroundType backgroundType = BackgroundType.Default;

        public override void Initialize(GenerationContext context) { }

        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            var bgData = new TileData
            {
                type = TileType.Solid,
                layer = TileLayer.Background,
                backgroundType = backgroundType
            };

            for (int x = 0; x < chunk.width; x++)
            {
                // Explicitly use highest Ground or Platform tile so we fill under both
                int highestY = chunk.GetHighestSolidHeight(x);
                int fillTop = highestY - 1; // one below top edge, fill everything down
                if (fillTop >= 0)
                    chunk.FillBackgroundColumn(x, 0, fillTop, bgData);
            }

            return chunk;
        }
    }
}
