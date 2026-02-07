using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Fügt Rampen ein wo Höhenunterschiede von 1 existieren.
    /// Arbeitet auf der fertigen Matrix und ersetzt Top-Tiles durch Rampen.
    /// </summary>
    public class RampPass : GeneratorPassBase
    {
        [Header("Ramp Settings")]
        [Tooltip("Nur Ground-Layer oder auch Plattformen?")]
        public bool applyToGround = true;
        public bool applyToPlatforms = true;
        
        [Header("Detection")]
        [Tooltip("Nur bei Höhendifferenz von genau 1 Rampen setzen")]
        public bool onlyForSingleSteps = true;
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            // Surface Heights müssen existieren
            if (chunk.metadata.surfaceHeights == null)
                return chunk;
            
            for (int x = 0; x < chunk.width; x++)
            {
                int currentHeight = chunk.metadata.surfaceHeights[x];
                if (currentHeight < 0) continue;
                
                // Vorherige Höhe holen
                int previousHeight = GetPreviousHeight(chunk, context, x);
                if (previousHeight < 0) continue;
                
                int heightDiff = currentHeight - previousHeight;
                
                // Nur bei Höhendifferenz von 1 (oder -1)
                if (onlyForSingleSteps && Mathf.Abs(heightDiff) != 1)
                    continue;
                
                // Prüfen ob es kein Gap ist
                TileData currentTile = chunk[x, currentHeight - 1];
                if (currentTile.type == TileType.Gap || currentTile.type == TileType.Air)
                    continue;
                
                // Layer-Filter
                if (!ShouldApplyToLayer(currentTile.layer))
                    continue;
                
                // Rampe setzen
                if (heightDiff == 1)
                {
                    // Aufwärts: Rampe an previousHeight setzen
                    SetRamp(chunk, x, previousHeight, true, currentTile.layer);
                }
                else if (heightDiff == -1)
                {
                    // Abwärts: Rampe an currentHeight setzen
                    SetRamp(chunk, x, currentHeight, false, currentTile.layer);
                }
            }
            
            return chunk;
        }
        
        int GetPreviousHeight(ChunkData chunk, GenerationContext context, int localX)
        {
            // Vorherige Spalte im selben Chunk
            if (localX > 0)
            {
                return chunk.metadata.surfaceHeights[localX - 1];
            }
            
            // Linker Nachbar-Chunk
            if (context.leftNeighbor != null && context.leftNeighbor.metadata.isComplete)
            {
                return context.leftNeighbor.metadata.rightEdgeHeight;
            }
            
            return -1;
        }
        
        void SetRamp(ChunkData chunk, int x, int y, bool up, TileLayer layer)
        {
            if (!chunk.IsInBounds(x, y)) return;
            
            TileData ramp = TileData.Ramp(up, layer);
            ramp.heightLevel = y;
            ramp.flags |= TileFlags.EdgeTop;
            
            chunk[x, y] = ramp;
        }
        
        bool ShouldApplyToLayer(TileLayer layer)
        {
            switch (layer)
            {
                case TileLayer.Ground:
                    return applyToGround;
                case TileLayer.Platform:
                    return applyToPlatforms;
                default:
                    return true;
            }
        }
        
        void Reset()
        {
            passName = "Ramp Pass";
        }
    }
}
