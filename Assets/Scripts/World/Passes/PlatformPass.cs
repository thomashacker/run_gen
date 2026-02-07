using UnityEngine;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Generiert schwebende Plattformen über dem Boden.
    /// Unterstützt mehrere Ebenen mit eigenen Einstellungen.
    /// </summary>
    public class PlatformPass : GeneratorPassBase
    {
        [System.Serializable]
        public class PlatformLayer
        {
            public string name = "Layer";
            public bool enabled = true;
            
            [Header("Height Above Ground")]
            public int minHeightAboveGround = 4;
            public int maxHeightAboveGround = 8;
            
            [Header("Noise")]
            [Range(0.01f, 0.3f)]
            public float noiseFrequency = 0.06f;
            public float noiseSeedOffset = 0f;
            
            [Header("Dimensions")]
            public int minLength = 4;
            public int maxLength = 12;
            public int minThickness = 1;
            public int maxThickness = 2;
            
            [Header("Gaps")]
            [Range(0f, 1f)]
            public float gapChance = 0.4f;
            public int minGapWidth = 4;
            public int maxGapWidth = 10;
            
            [Header("Spawn")]
            [Range(0f, 1f)]
            public float spawnDensity = 0.6f;
            public int safeStartColumns = 20;
            
            // Runtime State
            [HideInInspector] public int totalColumns;
            [HideInInspector] public int platformRemaining;
            [HideInInspector] public int gapRemaining;
            [HideInInspector] public int currentThickness;
            [HideInInspector] public int lastHeight;
            [HideInInspector] public bool isInPlatform;
        }
        
        [Header("Platform Layers")]
        public List<PlatformLayer> layers = new List<PlatformLayer>();
        
        [Header("Ground Interaction")]
        [Tooltip("Minimaler Abstand zum Boden")]
        public int minDistanceToGround = 2;
        
        public override void Initialize(GenerationContext context)
        {
            foreach (var layer in layers)
            {
                layer.totalColumns = 0;
                layer.platformRemaining = 0;
                layer.gapRemaining = 0;
                layer.isInPlatform = false;
                layer.lastHeight = layer.minHeightAboveGround;
            }
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                // Ground-Höhe an dieser Stelle
                int groundHeight = chunk.metadata.surfaceHeights != null && x < chunk.metadata.surfaceHeights.Length
                    ? chunk.metadata.surfaceHeights[x]
                    : 3;
                
                // Jeden Layer verarbeiten
                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (!layer.enabled) continue;
                    
                    ProcessLayerColumn(chunk, layer, x, worldX, groundHeight, context, i);
                }
            }
            
            return chunk;
        }
        
        void ProcessLayerColumn(ChunkData chunk, PlatformLayer layer, int localX, int worldX, 
            int groundHeight, GenerationContext context, int layerIndex)
        {
            layer.totalColumns++;
            
            // Safe Start
            if (layer.totalColumns <= layer.safeStartColumns)
                return;
            
            // Im Gap?
            if (layer.gapRemaining > 0)
            {
                layer.gapRemaining--;
                layer.isInPlatform = false;
                return;
            }
            
            // Plattform fortsetzen?
            if (layer.platformRemaining > 0)
            {
                int height = GetLayerHeight(layer, worldX, groundHeight, context, layerIndex);
                
                // Ground-Kollision prüfen
                if (height < minDistanceToGround)
                    height = minDistanceToGround;
                
                int platformY = groundHeight + height;
                
                SetPlatformTiles(chunk, localX, platformY, layer.currentThickness);
                
                layer.lastHeight = height;
                layer.platformRemaining--;
                layer.isInPlatform = true;
                return;
            }
            
            // Gap starten?
            if (layer.isInPlatform && Random.value < layer.gapChance)
            {
                layer.gapRemaining = Random.Range(layer.minGapWidth, layer.maxGapWidth + 1);
                layer.isInPlatform = false;
                return;
            }
            
            // Neue Plattform starten?
            if (Random.value < layer.spawnDensity)
            {
                layer.platformRemaining = Random.Range(layer.minLength, layer.maxLength + 1);
                layer.currentThickness = Random.Range(layer.minThickness, layer.maxThickness + 1);
                
                int height = GetLayerHeight(layer, worldX, groundHeight, context, layerIndex);
                if (height < minDistanceToGround)
                    height = minDistanceToGround;
                
                int platformY = groundHeight + height;
                
                SetPlatformTiles(chunk, localX, platformY, layer.currentThickness);
                
                layer.lastHeight = height;
                layer.platformRemaining--;
                layer.isInPlatform = true;
            }
        }
        
        void SetPlatformTiles(ChunkData chunk, int localX, int topY, int thickness)
        {
            for (int y = topY - thickness + 1; y <= topY; y++)
            {
                if (y >= 0 && chunk.IsInBounds(localX, y))
                {
                    var tile = TileData.Solid(TileLayer.Platform);
                    tile.type = TileType.Platform;
                    tile.heightLevel = y;
                    
                    if (y == topY)
                        tile.flags |= TileFlags.EdgeTop;
                    
                    chunk[localX, y] = tile;
                }
            }
        }
        
        int GetLayerHeight(PlatformLayer layer, int worldX, int groundHeight, 
            GenerationContext context, int layerIndex)
        {
            float noise = context.GetFractalNoise(
                worldX, 
                layer.noiseFrequency, 
                2, 
                layer.noiseSeedOffset + layerIndex * 100f
            );
            
            return Mathf.RoundToInt(Mathf.Lerp(
                layer.minHeightAboveGround, 
                layer.maxHeightAboveGround, 
                noise
            ));
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
                    maxHeightAboveGround = 5,
                    spawnDensity = 0.5f
                });
                
                layers.Add(new PlatformLayer
                {
                    name = "Upper Platforms",
                    minHeightAboveGround = 7,
                    maxHeightAboveGround = 10,
                    noiseSeedOffset = 50f,
                    spawnDensity = 0.3f,
                    minLength = 3,
                    maxLength = 8
                });
            }
        }
    }
}
