using UnityEngine;
using UnityEngine.Tilemaps;

namespace WorldGeneration
{
    /// <summary>
    /// Typ eines Tiles in der Chunk-Matrix.
    /// </summary>
    public enum TileType
    {
        Air,            // Leer / Luft
        Solid,          // Normaler Block
        RampUp,         // Rampe aufwärts (links-unten → rechts-oben)
        RampDown,       // Rampe abwärts (links-oben → rechts-unten)
        Gap,            // Explizites Loch (für Gap Pass)
        Platform,       // Plattform-Tile (kann anders gerendert werden)
        // Erweiterbar:
        // Decoration,
        // Hazard,
        // Collectible,
    }
    
    /// <summary>
    /// Layer/Ebene eines Tiles (für Rendering und Pass-Logik).
    /// </summary>
    public enum TileLayer
    {
        None,
        Ground,         // Hauptboden
        Platform,       // Schwebende Plattformen
        Foreground,     // Vordergrund-Dekoration
        Background,     // Hintergrund
    }
    
    /// <summary>
    /// Daten für eine einzelne Zelle in der Chunk-Matrix.
    /// Erweiterbar für zukünftige Features.
    /// </summary>
    [System.Serializable]
    public struct TileData
    {
        public TileType type;
        public TileLayer layer;
        
        /// <summary>
        /// Optional: Spezifisches Tile-Asset überschreiben.
        /// Wenn null, wird das Default-Tile des Layers verwendet.
        /// </summary>
        public TileBase overrideTile;
        
        /// <summary>
        /// Höhe relativ zum Chunk-Boden (nützlich für Passes).
        /// </summary>
        public int heightLevel;
        
        /// <summary>
        /// Flags für zusätzliche Eigenschaften.
        /// </summary>
        public TileFlags flags;
        
        // === FACTORY METHODS ===
        
        public static TileData Air => new TileData 
        { 
            type = TileType.Air, 
            layer = TileLayer.None 
        };
        
        public static TileData Solid(TileLayer layer = TileLayer.Ground) => new TileData 
        { 
            type = TileType.Solid, 
            layer = layer 
        };
        
        public static TileData Ramp(bool up, TileLayer layer = TileLayer.Ground) => new TileData 
        { 
            type = up ? TileType.RampUp : TileType.RampDown, 
            layer = layer 
        };
        
        public static TileData Gap => new TileData 
        { 
            type = TileType.Gap, 
            layer = TileLayer.None 
        };
        
        // === HELPER PROPERTIES ===
        
        public bool IsEmpty => type == TileType.Air || type == TileType.Gap;
        public bool IsSolid => type == TileType.Solid || type == TileType.Platform;
        public bool IsRamp => type == TileType.RampUp || type == TileType.RampDown;
        public bool IsWalkable => IsSolid || IsRamp;
    }
    
    /// <summary>
    /// Zusätzliche Flags für Tiles (bitweise kombinierbar).
    /// </summary>
    [System.Flags]
    public enum TileFlags
    {
        None = 0,
        Modified = 1 << 0,      // Von einem Pass modifiziert
        Protected = 1 << 1,     // Darf nicht überschrieben werden
        EdgeLeft = 1 << 2,      // Linke Kante
        EdgeRight = 1 << 3,     // Rechte Kante
        EdgeTop = 1 << 4,       // Obere Kante (Oberfläche)
        // Erweiterbar...
    }
}
