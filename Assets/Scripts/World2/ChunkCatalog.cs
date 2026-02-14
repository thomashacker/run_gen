using UnityEngine;
using System.Collections.Generic;

namespace World2
{
    /// <summary>
    /// A named group of chunks that is active during a specific distance range.
    /// Multiple groups can overlap -- all eligible chunks from all active groups
    /// are pooled together for weighted-random selection.
    /// </summary>
    [System.Serializable]
    public class ChunkGroup
    {
        [Tooltip("Label for this group (e.g., 'Easy Start', 'Mid Game', 'Endgame').")]
        public string name = "New Group";

        [Tooltip("World scroll distance at which this group starts being eligible.")]
        [Min(0f)]
        public float startDistance = 0f;

        [Tooltip("World scroll distance at which this group stops being eligible. Set to -1 for 'forever'.")]
        public float endDistance = -1f;

        [Tooltip("Chunk definitions in this group. Each has its own spawn weight.")]
        public List<ChunkDefinition> chunks = new List<ChunkDefinition>();
    }

    /// <summary>
    /// ScriptableObject that holds groups of ChunkDefinitions.
    /// Each group is active during a configurable distance range.
    /// At any given distance, all chunks from all active groups are pooled
    /// and one is picked via weighted-random selection.
    /// 
    /// Create via: Create > World2 > Chunk Catalog
    /// </summary>
    [CreateAssetMenu(fileName = "ChunkCatalog", menuName = "World2/Chunk Catalog")]
    public class ChunkCatalog : ScriptableObject
    {
        [Header("Chunk Groups")]
        [Tooltip("Define groups with distance ranges. Groups can overlap.")]
        public List<ChunkGroup> groups = new List<ChunkGroup>();

        /// <summary>
        /// Returns a weighted-random ChunkDefinition from all groups active at the given distance.
        /// Falls back to any valid chunk if nothing is eligible.
        /// </summary>
        /// <summary>
        /// Returns a weighted-random ChunkDefinition from all groups active at the given distance.
        /// Will not return the same chunk as lastChunk (unless it's the only option).
        /// </summary>
        public ChunkDefinition GetRandomChunk(float currentDistance, ChunkDefinition lastChunk = null)
        {
            // Collect eligible chunks from all active groups
            List<ChunkDefinition> eligible = new List<ChunkDefinition>();
            float totalWeight = 0f;

            foreach (var group in groups)
            {
                if (!IsGroupActive(group, currentDistance)) continue;

                foreach (var chunk in group.chunks)
                {
                    if (chunk == null || chunk.prefab == null) continue;
                    if (chunk == lastChunk) continue; // skip duplicate
                    eligible.Add(chunk);
                    totalWeight += chunk.spawnWeight;
                }
            }

            // If excluding lastChunk left us empty, allow it as fallback
            if (eligible.Count == 0 && lastChunk != null)
                return lastChunk;

            // Fallback: if nothing eligible at all, return any valid chunk
            if (eligible.Count == 0)
            {
                Debug.LogWarning($"[ChunkCatalog] No eligible chunks at distance {currentDistance:F0}. Using fallback.");
                return GetAnyValidChunk();
            }

            // Weighted random selection
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var chunk in eligible)
            {
                cumulative += chunk.spawnWeight;
                if (roll <= cumulative)
                    return chunk;
            }

            return eligible[eligible.Count - 1];
        }

        /// <summary>
        /// Returns all chunks matching the given tag across all groups.
        /// </summary>
        public List<ChunkDefinition> GetChunksWithTag(string tag)
        {
            var result = new List<ChunkDefinition>();
            foreach (var group in groups)
            {
                foreach (var chunk in group.chunks)
                {
                    if (chunk == null || chunk.tags == null) continue;
                    foreach (var t in chunk.tags)
                    {
                        if (t == tag)
                        {
                            result.Add(chunk);
                            break;
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the total number of valid chunk definitions across all groups.
        /// </summary>
        public int ValidChunkCount
        {
            get
            {
                int count = 0;
                foreach (var group in groups)
                {
                    foreach (var chunk in group.chunks)
                    {
                        if (chunk != null && chunk.prefab != null)
                            count++;
                    }
                }
                return count;
            }
        }

        // ─── Internal ───────────────────────────────────────────────

        static bool IsGroupActive(ChunkGroup group, float distance)
        {
            if (distance < group.startDistance) return false;
            if (group.endDistance >= 0f && distance > group.endDistance) return false;
            return true;
        }

        ChunkDefinition GetAnyValidChunk()
        {
            foreach (var group in groups)
            {
                foreach (var chunk in group.chunks)
                {
                    if (chunk != null && chunk.prefab != null)
                        return chunk;
                }
            }
            return null;
        }
    }
}
