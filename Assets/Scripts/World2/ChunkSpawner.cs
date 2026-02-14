using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace World2
{
    /// <summary>
    /// Manages the chunk lifecycle for a left-scrolling world with seamless Rule Tiles.
    /// 
    /// Architecture:
    ///   - A WorldScroller transform moves leftward each frame. It contains:
    ///       1. A shared Grid with shared Tilemaps (Ground, Platforms, Background).
    ///          All chunk tiles are stamped here so Rule Tiles see continuous neighbors.
    ///       2. Entity instances per chunk (enemies, traps, collectibles) with their
    ///          TilemapRenderers disabled -- only the entity GameObjects are active.
    ///   - Kill zones and camera are static.
    /// 
    /// Each frame:
    ///   1. Moves the WorldScroller left at AutoScrollController.CurrentSpeed
    ///   2. Stamps new chunk tiles onto the shared tilemaps when the right edge needs filling
    ///   3. Clears tiles and recycles entity instances that scroll past the left edge
    /// 
    /// Setup:
    ///   1. Create a "WorldScroller" GameObject in the scene
    ///   2. Under it, add a Grid with child Tilemaps named "Ground", "Platforms", "Background"
    ///   3. Assign the WorldScroller transform and the shared Tilemaps in the Inspector
    ///   4. Assign a ChunkCatalog with your chunk definitions
    ///   5. Ensure chunk prefab tilemaps are named to match: "Ground", "Platforms", "Background"
    /// </summary>
    public class ChunkSpawner : MonoBehaviour
    {
        public static ChunkSpawner Instance { get; private set; }

        [Header("Chunk Settings")]
        [Tooltip("Width of each chunk in tiles. Must match WorldChunk.widthInTiles on all prefabs.")]
        public int chunkWidthTiles = 20;

        [Tooltip("Height used when clearing tile regions. Should cover the tallest chunk.")]
        public int chunkHeightTiles = 25;

        [Header("Spawn / Despawn")]
        [Tooltip("Chunk-widths past the right camera edge to keep filled.")]
        public float spawnBufferRight = 1.5f;

        [Tooltip("Chunk-widths past the left camera edge before despawning.")]
        public float despawnBufferLeft = 1f;

        [Header("References")]
        public ChunkCatalog catalog;

        [Tooltip("Leave null to use Camera.main.")]
        public Camera viewCamera;

        [Header("World Scroller")]
        [Tooltip("Parent transform that scrolls left. Contains the shared Grid and entity instances.")]
        public Transform worldScroller;

        [Header("Shared Tilemaps (children of WorldScroller's Grid)")]
        [Tooltip("Shared ground Tilemap. All chunk ground tiles are stamped here.")]
        public Tilemap sharedGround;

        [Tooltip("Shared platforms Tilemap (optional).")]
        public Tilemap sharedPlatforms;

        [Tooltip("Shared background Tilemap (optional).")]
        public Tilemap sharedBackground;

        // Camera bounds (static camera, computed once)
        private float cameraLeftEdge;
        private float cameraRightEdge;

        // Child Rigidbody2Ds (created by CompositeCollider2D on tilemaps)
        private Rigidbody2D[] childBodies;

        // Chunk tracking
        private float chunkWorldWidth;
        private int nextTileOffsetX;        // tile X where the next chunk will be stamped
        private int chunksSpawnedTotal;
        private ChunkDefinition lastSpawnedChunk;
        private readonly List<ActiveChunk> activeChunks = new List<ActiveChunk>();

        // Entity instance pool: keyed by prefab asset
        private readonly Dictionary<GameObject, Queue<GameObject>> entityPool =
            new Dictionary<GameObject, Queue<GameObject>>();

        private struct ActiveChunk
        {
            public ChunkDefinition definition;
            public int tileOffsetX;             // tile X origin of the stamp
            public GameObject entityInstance;    // prefab clone (tilemap renderers off, entities on)
        }

        // ─── Lifecycle ──────────────────────────────────────────────

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            ComputeCameraBounds();

            chunkWorldWidth = chunkWidthTiles * GetCellSize().x;

            // CompositeCollider2D on the shared tilemaps auto-creates a Rigidbody2D
            // (defaults to Static). Switch those to Kinematic so the physics engine
            // properly tracks velocity when the parent transform moves, preventing
            // tunneling at high speeds.
            if (worldScroller != null)
            {
                childBodies = worldScroller.GetComponentsInChildren<Rigidbody2D>();
                foreach (var rb in childBodies)
                {
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                }
            }

            // Start stamping from a tile position that aligns with the left camera edge
            nextTileOffsetX = TileXFromWorldX(cameraLeftEdge);
            chunksSpawnedTotal = 0;

            // Pre-cache tile data from every chunk prefab
            ChunkPrefabReader.PreCacheAll(catalog);

            // Fill the initial view
            SpawnInitialChunks();
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
                return;
            if (catalog == null || worldScroller == null) return;

            // --- 1. Scroll the world leftward ---
            // We move via transform. The child Kinematic Rigidbody2Ds (on the tilemaps)
            // automatically track the transform change and compute proper collision.
            float scrollSpeed = AutoScrollController.Instance != null
                ? AutoScrollController.Instance.CurrentSpeed
                : 0f;
            float delta = scrollSpeed * Time.deltaTime;
            worldScroller.position += Vector3.left * delta;

            // --- 2. Spawn new chunks when the right side needs filling ---
            float spawnThreshold = cameraRightEdge + spawnBufferRight * chunkWorldWidth;
            while (WorldXFromTileX(nextTileOffsetX) < spawnThreshold)
            {
                SpawnNextChunk();
            }

            // --- Despawn chunks that scrolled past the left edge ---
            float despawnThreshold = cameraLeftEdge - despawnBufferLeft * chunkWorldWidth;
            DespawnChunksBehind(despawnThreshold);
        }

        // ─── Spawning ───────────────────────────────────────────────

        void SpawnInitialChunks()
        {
            float spawnThreshold = cameraRightEdge + spawnBufferRight * chunkWorldWidth;

            while (WorldXFromTileX(nextTileOffsetX) < spawnThreshold)
            {
                SpawnNextChunk();
            }
        }

        void SpawnNextChunk()
        {
            float distance = GetCurrentScrollDistance();
            SpawnChunk(distance);
        }

        void SpawnChunk(float distance)
        {
            ChunkDefinition def = catalog.GetRandomChunk(distance, lastSpawnedChunk);
            if (def == null || def.prefab == null)
            {
                Debug.LogError("[ChunkSpawner] No valid chunk definition found! Skipping slot.");
                nextTileOffsetX += chunkWidthTiles;
                chunksSpawnedTotal++;
                return;
            }

            int tileX = nextTileOffsetX;

            // --- A. Stamp tiles onto shared tilemaps ---
            StampTiles(def, tileX);

            // --- B. Instantiate entity instance (or pull from pool) ---
            GameObject entityInst = GetEntityInstance(def);
            DisableTilemapRendering(entityInst);

            // Position so entities align with the stamped tiles.
            // Tile (tileX, 0) in the shared tilemap = local position (tileX * cellSize.x, 0).
            // The entity instance's own children are at local positions relative to the prefab root,
            // which correspond to tile positions 0..chunkWidth. We offset by tileX * cellSize.
            Vector2 cellSize = GetCellSize();
            entityInst.transform.SetParent(worldScroller, false);
            entityInst.transform.localPosition = new Vector3(tileX * cellSize.x, 0f, 0f);
            entityInst.SetActive(true);

            // Track
            activeChunks.Add(new ActiveChunk
            {
                definition = def,
                tileOffsetX = tileX,
                entityInstance = entityInst,
            });

            lastSpawnedChunk = def;
            nextTileOffsetX += chunkWidthTiles;
            chunksSpawnedTotal++;
        }

        /// <summary>
        /// Writes cached tile data from a chunk blueprint onto the shared tilemaps.
        /// </summary>
        void StampTiles(ChunkDefinition def, int offsetX)
        {
            var blueprint = ChunkPrefabReader.GetBlueprint(def);
            if (blueprint == null) return;

            foreach (var layer in blueprint.layers)
            {
                Tilemap target = GetSharedTilemap(layer.name);
                if (target == null) continue;

                // Offset the original bounds to the stamp position
                BoundsInt stampBounds = new BoundsInt(
                    layer.bounds.x + offsetX,
                    layer.bounds.y,
                    layer.bounds.z,
                    layer.bounds.size.x,
                    layer.bounds.size.y,
                    layer.bounds.size.z
                );

                target.SetTilesBlock(stampBounds, layer.tiles);
            }
        }

        // ─── Despawning ─────────────────────────────────────────────

        void DespawnChunksBehind(float worldThreshold)
        {
            for (int i = activeChunks.Count - 1; i >= 0; i--)
            {
                var chunk = activeChunks[i];

                // Right edge of this chunk in world space
                float chunkRightWorld = WorldXFromTileX(chunk.tileOffsetX + chunkWidthTiles);

                if (chunkRightWorld < worldThreshold)
                {
                    ClearTileRegion(chunk.tileOffsetX);
                    ReturnEntityInstance(chunk.definition, chunk.entityInstance);
                    activeChunks.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Fills a chunk-sized tile region with null tiles (erases).
        /// </summary>
        void ClearTileRegion(int offsetX)
        {
            ClearRegionOnTilemap(sharedGround, offsetX);
            ClearRegionOnTilemap(sharedPlatforms, offsetX);
            ClearRegionOnTilemap(sharedBackground, offsetX);
        }

        void ClearRegionOnTilemap(Tilemap tm, int offsetX)
        {
            if (tm == null) return;

            BoundsInt clearBounds = new BoundsInt(
                offsetX, 0, 0,
                chunkWidthTiles, chunkHeightTiles, 1
            );

            TileBase[] empty = new TileBase[chunkWidthTiles * chunkHeightTiles];
            tm.SetTilesBlock(clearBounds, empty);
        }

        // ─── Entity Instance Pooling ────────────────────────────────

        GameObject GetEntityInstance(ChunkDefinition def)
        {
            if (entityPool.TryGetValue(def.prefab, out var queue) && queue.Count > 0)
                return queue.Dequeue();

            return Instantiate(def.prefab);
        }

        void ReturnEntityInstance(ChunkDefinition def, GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);

            if (!entityPool.ContainsKey(def.prefab))
                entityPool[def.prefab] = new Queue<GameObject>();

            entityPool[def.prefab].Enqueue(instance);
        }

        /// <summary>
        /// Disables all TilemapRenderer, TilemapCollider2D, and CompositeCollider2D
        /// on a chunk instance so tiles aren't double-rendered or double-collided.
        /// The shared tilemaps handle rendering and collision instead.
        /// </summary>
        static void DisableTilemapRendering(GameObject chunkInstance)
        {
            foreach (var r in chunkInstance.GetComponentsInChildren<TilemapRenderer>(true))
                r.enabled = false;

            foreach (var c in chunkInstance.GetComponentsInChildren<TilemapCollider2D>(true))
                c.enabled = false;

            foreach (var c in chunkInstance.GetComponentsInChildren<CompositeCollider2D>(true))
                c.enabled = false;
        }

        // ─── Coordinate Helpers ─────────────────────────────────────

        /// <summary>
        /// Converts a tile X coordinate to world X, accounting for the scroller's position.
        /// </summary>
        float WorldXFromTileX(int tileX)
        {
            return worldScroller.position.x + tileX * GetCellSize().x;
        }

        /// <summary>
        /// Converts a world X to the nearest tile X in scroller-local tile space.
        /// </summary>
        int TileXFromWorldX(float worldX)
        {
            float localX = worldX - worldScroller.position.x;
            return Mathf.FloorToInt(localX / GetCellSize().x);
        }

        Vector2 GetCellSize()
        {
            if (sharedGround != null)
            {
                Grid grid = sharedGround.layoutGrid;
                if (grid != null) return grid.cellSize;
            }
            return Vector2.one;
        }

        /// <summary>
        /// Maps a prefab tilemap layer name to the corresponding shared tilemap.
        /// Matching is case-insensitive and substring-based.
        /// </summary>
        Tilemap GetSharedTilemap(string layerName)
        {
            string lower = layerName.ToLower();
            if (lower.Contains("ground")) return sharedGround;
            if (lower.Contains("platform")) return sharedPlatforms;
            if (lower.Contains("background")) return sharedBackground;

            // Unknown layer name -> default to ground
            return sharedGround;
        }

        float GetCurrentScrollDistance()
        {
            if (AutoScrollController.Instance != null)
                return AutoScrollController.Instance.TotalScrolled;
            return 0f;
        }

        void ComputeCameraBounds()
        {
            if (viewCamera == null) return;

            if (viewCamera.orthographic)
            {
                float halfHeight = viewCamera.orthographicSize;
                float halfWidth = halfHeight * viewCamera.aspect;
                cameraLeftEdge = viewCamera.transform.position.x - halfWidth;
                cameraRightEdge = viewCamera.transform.position.x + halfWidth;
            }
            else
            {
                Vector3 bl = viewCamera.ViewportToWorldPoint(new Vector3(0, 0, -viewCamera.transform.position.z));
                Vector3 tr = viewCamera.ViewportToWorldPoint(new Vector3(1, 1, -viewCamera.transform.position.z));
                cameraLeftEdge = bl.x;
                cameraRightEdge = tr.x;
            }
        }

        // ─── Public API ─────────────────────────────────────────────

        /// <summary>Total chunks spawned since the start.</summary>
        public int TotalChunksSpawned => chunksSpawnedTotal;

        /// <summary>Number of currently active chunks in the scene.</summary>
        public int ActiveChunkCount => activeChunks.Count;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Vector2 cellSize = GetCellSize();
            float displayHeight = chunkHeightTiles * cellSize.y;

            // Active chunk boundaries
            Gizmos.color = Color.yellow;
            foreach (var chunk in activeChunks)
            {
                float wx = WorldXFromTileX(chunk.tileOffsetX);
                Gizmos.DrawWireCube(
                    new Vector3(wx + chunkWorldWidth / 2f, displayHeight / 2f, 0f),
                    new Vector3(chunkWorldWidth, displayHeight, 0f)
                );
            }

            // Camera bounds
            Gizmos.color = Color.white;
            Gizmos.DrawLine(new Vector3(cameraLeftEdge, -2f, 0f), new Vector3(cameraLeftEdge, displayHeight, 0f));
            Gizmos.DrawLine(new Vector3(cameraRightEdge, -2f, 0f), new Vector3(cameraRightEdge, displayHeight, 0f));

            // Next stamp position
            Gizmos.color = Color.green;
            float spawnWorldX = WorldXFromTileX(nextTileOffsetX);
            Gizmos.DrawLine(new Vector3(spawnWorldX, 0f, 0f), new Vector3(spawnWorldX, displayHeight, 0f));
        }
#endif
    }
}
