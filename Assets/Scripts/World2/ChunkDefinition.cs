using UnityEngine;

namespace World2
{
    /// <summary>
    /// ScriptableObject that defines a single hand-designed chunk variation.
    /// Create via: Create > World2 > Chunk Definition
    /// Assign a prefab and spawn weight, then add to a ChunkGroup inside a ChunkCatalog.
    /// </summary>
    [CreateAssetMenu(fileName = "NewChunkDefinition", menuName = "World2/Chunk Definition")]
    public class ChunkDefinition : ScriptableObject
    {
        [Header("Chunk Prefab")]
        [Tooltip("The prefab containing the hand-designed chunk (Grid + Tilemaps + Entities).")]
        public GameObject prefab;

        [Header("Spawning")]
        [Tooltip("Spawn weight among eligible chunks in the same group. Higher = more likely to be picked.")]
        [Min(0.1f)]
        public float spawnWeight = 1f;

        [Header("Tags (Optional)")]
        [Tooltip("Optional tags for filtering (e.g., 'gap', 'enemy', 'cave').")]
        public string[] tags;
    }
}
