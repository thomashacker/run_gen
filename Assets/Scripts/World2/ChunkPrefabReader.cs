using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace World2
{
    /// <summary>
    /// Reads tile data from chunk prefabs and caches it for runtime stamping.
    /// 
    /// On first access for a given prefab, temporarily instantiates it (inactive),
    /// reads all Tilemap layers via GetTilesBlock, then destroys the temp instance.
    /// Subsequent accesses return the cached data instantly.
    /// 
    /// Call PreCacheAll(catalog) at Start() to avoid hitches during gameplay.
    /// </summary>
    public static class ChunkPrefabReader
    {
        /// <summary>
        /// Cached tile data for one tilemap layer within a chunk.
        /// </summary>
        public struct TilemapLayerData
        {
            /// <summary>Name of the Tilemap GameObject (e.g., "Ground", "Platforms").</summary>
            public string name;

            /// <summary>Tile bounds in the prefab's local tile space.</summary>
            public BoundsInt bounds;

            /// <summary>Flat tile array matching the bounds (row-major).</summary>
            public TileBase[] tiles;
        }

        /// <summary>
        /// All cached tile data extracted from one chunk prefab.
        /// </summary>
        public class ChunkBlueprint
        {
            public List<TilemapLayerData> layers = new List<TilemapLayerData>();
        }

        // Cache keyed by prefab asset reference
        private static readonly Dictionary<GameObject, ChunkBlueprint> cache =
            new Dictionary<GameObject, ChunkBlueprint>();

        /// <summary>
        /// Returns the cached tile blueprint for a ChunkDefinition's prefab.
        /// Extracts and caches on first call.
        /// </summary>
        public static ChunkBlueprint GetBlueprint(ChunkDefinition def)
        {
            if (def == null || def.prefab == null) return null;
            return GetBlueprint(def.prefab);
        }

        /// <summary>
        /// Returns the cached tile blueprint for a prefab GameObject.
        /// Extracts and caches on first call.
        /// </summary>
        public static ChunkBlueprint GetBlueprint(GameObject prefab)
        {
            if (prefab == null) return null;

            if (cache.TryGetValue(prefab, out var existing))
                return existing;

            var blueprint = ExtractBlueprint(prefab);
            cache[prefab] = blueprint;
            return blueprint;
        }

        /// <summary>
        /// Pre-caches blueprints for every definition in a catalog.
        /// Call this at Start() to front-load the extraction cost.
        /// </summary>
        public static void PreCacheAll(ChunkCatalog catalog)
        {
            if (catalog == null) return;
            foreach (var group in catalog.groups)
            {
                foreach (var def in group.chunks)
                {
                    if (def != null && def.prefab != null)
                        GetBlueprint(def);
                }
            }
            Debug.Log($"[ChunkPrefabReader] Pre-cached {cache.Count} chunk blueprint(s).");
        }

        /// <summary>
        /// Clears the entire blueprint cache. Call on scene unload if needed.
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
        }

        // ─── Internal ───────────────────────────────────────────────

        static ChunkBlueprint ExtractBlueprint(GameObject prefab)
        {
            // Instantiate hidden so no Awake/Start/OnEnable runs on children
            var instance = Object.Instantiate(prefab);
            instance.SetActive(false);

            var blueprint = new ChunkBlueprint();

            // Read every Tilemap in the prefab hierarchy
            Tilemap[] tilemaps = instance.GetComponentsInChildren<Tilemap>(true);
            foreach (var tm in tilemaps)
            {
                tm.CompressBounds();
                BoundsInt bounds = tm.cellBounds;

                // Skip empty tilemaps
                if (bounds.size.x <= 0 || bounds.size.y <= 0)
                    continue;

                TileBase[] tiles = tm.GetTilesBlock(bounds);

                blueprint.layers.Add(new TilemapLayerData
                {
                    name = tm.gameObject.name,
                    bounds = bounds,
                    tiles = tiles
                });
            }

            Object.Destroy(instance);
            return blueprint;
        }
    }
}
