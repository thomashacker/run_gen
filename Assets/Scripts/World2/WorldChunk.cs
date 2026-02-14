using UnityEngine;
using UnityEngine.Tilemaps;

namespace World2
{
    /// <summary>
    /// Placed on the root GameObject of each hand-designed chunk prefab.
    /// Defines chunk dimensions and validates that edges meet the flat-ground constraint.
    /// 
    /// Design-time workflow:
    /// 1. Create a GameObject with a Grid child containing Tilemap children
    /// 2. Name tilemaps to match the shared scene tilemaps:
    ///    "Ground" (required), "Platforms" (optional), "Background" (optional)
    ///    The ChunkPrefabReader uses these names to map tiles to the correct shared tilemap.
    /// 3. Paint your tiles using the Tile Palette
    /// 4. Place enemies/traps/collectibles as child GameObjects of the root (NOT under Grid)
    /// 5. Add this component to the root
    /// 6. Ensure left and right edges have flat ground at edgeGroundHeight
    /// 7. Save as Prefab
    /// 
    /// At runtime, tiles are stamped onto a single shared tilemap (seamless Rule Tiles).
    /// Entity children are instantiated as regular GameObjects on the scrolling world.
    /// </summary>
    public class WorldChunk : MonoBehaviour
    {
        [Header("Chunk Dimensions")]
        [Tooltip("Width of this chunk in tiles. Must match ChunkSpawner.chunkWidthTiles.")]
        public int widthInTiles = 20;

        [Tooltip("Height of this chunk in tiles (visual bounds, not a hard limit).")]
        public int heightInTiles = 25;

        [Header("Edge Constraint")]
        [Tooltip("Required solid ground height at left and right edges (tiles from y=0 up). All chunks must use the same value so any chunk can follow any other.")]
        public int edgeGroundHeight = 3;

        /// <summary>
        /// World-space width of this chunk, computed from the child Grid's cell size.
        /// </summary>
        public float WorldWidth
        {
            get
            {
                Grid grid = GetComponentInChildren<Grid>();
                float cellSizeX = grid != null ? grid.cellSize.x : 1f;
                return widthInTiles * cellSizeX;
            }
        }

        /// <summary>
        /// World-space height of this chunk, computed from the child Grid's cell size.
        /// </summary>
        public float WorldHeight
        {
            get
            {
                Grid grid = GetComponentInChildren<Grid>();
                float cellSizeY = grid != null ? grid.cellSize.y : 1f;
                return heightInTiles * cellSizeY;
            }
        }

#if UNITY_EDITOR
        [Header("Editor Validation")]
        [Tooltip("Enable automatic edge validation when values change in Inspector.")]
        [SerializeField] private bool validateEdges = true;

        void OnValidate()
        {
            if (!validateEdges) return;
            ValidateEdges();
        }

        /// <summary>
        /// Checks that the left-most and right-most columns of the ground tilemap
        /// have solid tiles from y=0 up to edgeGroundHeight-1. Logs warnings if not.
        /// </summary>
        public void ValidateEdges()
        {
            Tilemap groundTilemap = GetGroundTilemap();
            if (groundTilemap == null) return;

            // Check left edge (x = 0)
            bool leftValid = true;
            for (int y = 0; y < edgeGroundHeight; y++)
            {
                if (!groundTilemap.HasTile(new Vector3Int(0, y, 0)))
                {
                    leftValid = false;
                    break;
                }
            }

            // Check right edge (x = widthInTiles - 1)
            bool rightValid = true;
            for (int y = 0; y < edgeGroundHeight; y++)
            {
                if (!groundTilemap.HasTile(new Vector3Int(widthInTiles - 1, y, 0)))
                {
                    rightValid = false;
                    break;
                }
            }

            if (!leftValid)
                Debug.LogWarning($"[WorldChunk] '{gameObject.name}' - Left edge missing ground tiles at x=0 (need {edgeGroundHeight} solid tiles from y=0).", this);
            if (!rightValid)
                Debug.LogWarning($"[WorldChunk] '{gameObject.name}' - Right edge missing ground tiles at x={widthInTiles - 1} (need {edgeGroundHeight} solid tiles from y=0).", this);
        }

        /// <summary>
        /// Finds the ground tilemap among children. Prefers one named "Ground".
        /// </summary>
        private Tilemap GetGroundTilemap()
        {
            Tilemap[] tilemaps = GetComponentsInChildren<Tilemap>();
            foreach (var tm in tilemaps)
            {
                string lowerName = tm.gameObject.name.ToLower();
                if (lowerName.Contains("ground"))
                    return tm;
            }
            return tilemaps.Length > 0 ? tilemaps[0] : null;
        }

        void OnDrawGizmosSelected()
        {
            Grid grid = GetComponentInChildren<Grid>();
            float cellX = grid != null ? grid.cellSize.x : 1f;
            float cellY = grid != null ? grid.cellSize.y : 1f;

            float width = widthInTiles * cellX;
            float height = heightInTiles * cellY;
            Vector3 origin = transform.position;

            // Chunk bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                origin + new Vector3(width / 2f, height / 2f, 0f),
                new Vector3(width, height, 0f)
            );

            // Edge ground requirement (left)
            float edgeH = edgeGroundHeight * cellY;
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Gizmos.DrawCube(
                origin + new Vector3(cellX / 2f, edgeH / 2f, 0f),
                new Vector3(cellX, edgeH, 0f)
            );
            // Edge ground requirement (right)
            Gizmos.DrawCube(
                origin + new Vector3(width - cellX / 2f, edgeH / 2f, 0f),
                new Vector3(cellX, edgeH, 0f)
            );
        }
#endif
    }
}
