using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// DEPRECATED: Use WorldGeneration.PlatformPass instead.
/// This script is kept for reference only.
/// </summary>
[System.Obsolete("Use WorldGeneration.PlatformPass instead")]
public class PlatformGenerator : MonoBehaviour
{
    [System.Serializable]
    public class PlatformLayer
    {
        public string name = "Layer 1";
        public bool enabled = true;
        
        [Header("Height Above Ground")]
        [Tooltip("Minimale Höhe über dem Boden (in Tiles)")]
        public int minHeightAboveGround = 4;
        [Tooltip("Maximale Höhe über dem Boden (in Tiles)")]
        public int maxHeightAboveGround = 8;
        
        [Header("Noise Settings")]
        [Range(0.01f, 0.3f)]
        public float noiseFrequency = 0.06f;
        public float noiseAmplitude = 3f;
        [Tooltip("Offset zum Ground-Noise (0 = synchron, andere Werte = versetzt)")]
        public float noisePhaseOffset = 0f;
        
        [Header("Platform Dimensions")]
        [Tooltip("Minimale Länge einer Plattform (horizontal)")]
        public int minLength = 4;
        [Tooltip("Maximale Länge einer Plattform (horizontal)")]
        public int maxLength = 12;
        [Tooltip("Minimale Dicke (vertikal, 1 = dünn)")]
        public int minThickness = 1;
        [Tooltip("Maximale Dicke (vertikal)")]
        public int maxThickness = 2;
        
        [Header("Gaps Between Platforms")]
        [Range(0f, 1f)]
        public float gapChance = 0.4f;
        public int minGapWidth = 4;
        public int maxGapWidth = 10;
        
        [Header("Spawn Control")]
        [Range(0f, 1f)]
        [Tooltip("Dichte der Plattformen (1 = durchgehend, 0 = keine)")]
        public float spawnDensity = 0.6f;
        public int safeStartColumns = 20;
        
        [Header("Ramps")]
        public bool useRamps = true;
        
        // Runtime State (nicht serialisiert)
        [HideInInspector] public float noiseSeed;
        [HideInInspector] public int lastHeight;
        [HideInInspector] public int previousHeight;
        [HideInInspector] public int platformRemaining;      // Verbleibende Spalten der aktuellen Plattform
        [HideInInspector] public int gapRemaining;           // Verbleibende Gap-Spalten
        [HideInInspector] public int currentThickness;       // Dicke der aktuellen Plattform
        [HideInInspector] public int totalColumns;
        [HideInInspector] public bool isInPlatform;
    }
    
    [Header("Tilemap")]
    public Tilemap tilemap;
    
    [Header("Tiles")]
    public TileBase platformTile;
    public TileBase rampUpTile;
    public TileBase rampDownTile;
    
    [Header("Platform Layers")]
    public List<PlatformLayer> layers = new List<PlatformLayer>();
    
    [Header("Ground Interaction")]
    [Tooltip("Minimaler Abstand zum Boden (wenn näher → merge oder skip)")]
    public int minDistanceToGround = 2;
    [Tooltip("Bei Kollision mit Boden: true = mit Boden verschmelzen, false = überspringen")]
    public bool mergeWithGround = true;
    
    [Header("Chunk Settings")]
    public int chunkWidth = 20;
    public int chunksAhead = 3;
    public int chunksBehind = 1;
    
    [Header("References")]
    public Transform player;
    public GroundGenerator groundGenerator;
    
    // Private
    private Grid grid;
    private float cellSizeX;
    private int leftmostChunkIndex = 0;
    private int rightmostChunkIndex = -1;
    
    // Cache für Ground-Höhen (um nicht ständig abzufragen)
    private Dictionary<int, int> groundHeightCache = new Dictionary<int, int>();
    
    void Awake()
    {
        if (tilemap == null)
            tilemap = GetComponent<Tilemap>();
        
        grid = GetComponentInParent<Grid>();
        if (grid == null && groundGenerator != null)
            grid = groundGenerator.GetComponentInParent<Grid>();
        
        cellSizeX = grid != null ? grid.cellSize.x : 1f;
        
        // Seeds für jeden Layer initialisieren
        foreach (var layer in layers)
        {
            layer.noiseSeed = Random.Range(0f, 10000f);
            layer.lastHeight = layer.minHeightAboveGround;
            layer.previousHeight = layer.minHeightAboveGround;
        }
    }
    
    void Start()
    {
        // Auto-find References
        if (player == null)
        {
            PlayerManager pm = FindAnyObjectByType<PlayerManager>();
            if (pm != null) player = pm.transform;
        }
        
        if (groundGenerator == null)
            groundGenerator = FindAnyObjectByType<GroundGenerator>();
        
        UpdateChunks();
    }
    
    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            return;
        
        if (player == null) return;
        
        UpdateChunks();
    }
    
    void UpdateChunks()
    {
        if (player == null) return;
        
        int playerChunk = Mathf.FloorToInt(player.position.x / (chunkWidth * cellSizeX));
        
        // Chunks vor dem Spieler generieren
        int targetRightChunk = playerChunk + chunksAhead;
        while (rightmostChunkIndex < targetRightChunk)
        {
            rightmostChunkIndex++;
            GenerateChunk(rightmostChunkIndex);
        }
        
        // Alte Chunks entfernen
        int targetLeftChunk = playerChunk - chunksBehind;
        while (leftmostChunkIndex < targetLeftChunk)
        {
            RemoveChunk(leftmostChunkIndex);
            leftmostChunkIndex++;
        }
    }
    
    void GenerateChunk(int chunkIndex)
    {
        int startX = chunkIndex * chunkWidth;
        for (int x = startX; x < startX + chunkWidth; x++)
        {
            GenerateColumn(x);
        }
    }
    
    void GenerateColumn(int x)
    {
        // Ground-Höhe cachen
        int groundHeight = GetGroundHeight(x);
        
        // Jeden Layer separat generieren
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (!layer.enabled) continue;
            
            layer.totalColumns++;
            
            // Safe Start - keine Plattformen am Anfang
            if (layer.totalColumns <= layer.safeStartColumns)
                continue;
            
            GenerateLayerColumn(layer, x, groundHeight, i);
        }
    }
    
    void GenerateLayerColumn(PlatformLayer layer, int x, int groundHeight, int layerIndex)
    {
        // === GAP HANDLING ===
        if (layer.gapRemaining > 0)
        {
            // Ende der Plattform - Rampe runter?
            if (layer.gapRemaining == layer.minGapWidth && layer.useRamps && layer.isInPlatform)
            {
                // Rampe am Ende der vorherigen Plattform wurde schon gesetzt
            }
            
            layer.gapRemaining--;
            layer.isInPlatform = false;
            layer.previousHeight = layer.lastHeight;
            return;
        }
        
        // === PLATFORM HANDLING ===
        if (layer.platformRemaining > 0)
        {
            // Plattform fortsetzen
            int platformY = groundHeight + layer.lastHeight;
            
            // Ground-Kollision prüfen
            if (platformY <= groundHeight + minDistanceToGround)
            {
                if (mergeWithGround)
                {
                    // Merge: Plattform-Tiles bis zum Boden
                    platformY = groundHeight + minDistanceToGround;
                }
                else
                {
                    // Skip: Diese Spalte überspringen
                    layer.platformRemaining--;
                    return;
                }
            }
            
            // Rampen-Check am Anfang/Ende der Plattform
            bool isFirstColumn = !layer.isInPlatform;
            bool isLastColumn = layer.platformRemaining == 1;
            
            // Höhenänderung innerhalb der Plattform (sanfte Wellen)
            int newHeight = GetLayerHeight(layer, x, groundHeight);
            int heightDiff = newHeight - layer.previousHeight;
            
            // Slope begrenzen für faire Plattformen
            newHeight = Mathf.Clamp(newHeight, layer.previousHeight - 1, layer.previousHeight + 1);
            heightDiff = newHeight - layer.previousHeight;
            
            platformY = groundHeight + newHeight;
            
            // Tiles setzen
            SetPlatformTiles(layer, x, platformY, heightDiff, isFirstColumn, isLastColumn);
            
            layer.previousHeight = newHeight;
            layer.lastHeight = newHeight;
            layer.platformRemaining--;
            layer.isInPlatform = true;
            return;
        }
        
        // === NEUE PLATTFORM ODER GAP STARTEN ===
        
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
            // Plattform-Parameter bestimmen
            layer.platformRemaining = Random.Range(layer.minLength, layer.maxLength + 1);
            layer.currentThickness = Random.Range(layer.minThickness, layer.maxThickness + 1);
            
            // Höhe für diese Plattform
            int newHeight = GetLayerHeight(layer, x, groundHeight);
            layer.lastHeight = newHeight;
            
            int platformY = groundHeight + newHeight;
            
            // Ground-Kollision prüfen
            if (platformY <= groundHeight + minDistanceToGround)
            {
                if (!mergeWithGround)
                {
                    // Keine Plattform hier
                    layer.platformRemaining = 0;
                    return;
                }
                platformY = groundHeight + minDistanceToGround;
                layer.lastHeight = minDistanceToGround;
            }
            
            // Erste Spalte setzen (mit Anfangs-Rampe wenn enabled)
            SetPlatformTiles(layer, x, platformY, 0, true, false);
            
            layer.previousHeight = layer.lastHeight;
            layer.platformRemaining--;
            layer.isInPlatform = true;
        }
    }
    
    void SetPlatformTiles(PlatformLayer layer, int x, int platformY, int heightDiff, bool isFirst, bool isLast)
    {
        bool canUseRamps = layer.useRamps && rampUpTile != null && rampDownTile != null;
        
        int thickness = layer.currentThickness;
        
        // Obere Tile (Oberfläche) - kann Rampe sein
        if (canUseRamps && heightDiff == 1)
        {
            // Aufwärts-Rampe
            tilemap.SetTile(new Vector3Int(x, platformY, 0), rampUpTile);
            // Tiles darunter
            for (int y = platformY - thickness; y < platformY; y++)
            {
                if (y >= 0)
                    tilemap.SetTile(new Vector3Int(x, y, 0), platformTile);
            }
        }
        else if (canUseRamps && heightDiff == -1)
        {
            // Abwärts-Rampe
            tilemap.SetTile(new Vector3Int(x, platformY + 1, 0), rampDownTile);
            // Tiles darunter (normale Höhe)
            for (int y = platformY - thickness + 1; y <= platformY; y++)
            {
                if (y >= 0)
                    tilemap.SetTile(new Vector3Int(x, y, 0), platformTile);
            }
        }
        else
        {
            // Normale Tiles (keine Rampe)
            for (int y = platformY - thickness + 1; y <= platformY; y++)
            {
                if (y >= 0)
                    tilemap.SetTile(new Vector3Int(x, y, 0), platformTile);
            }
        }
    }
    
    int GetLayerHeight(PlatformLayer layer, int x, int groundHeight)
    {
        // Noise-basierte Höhe für diesen Layer
        float noise = GetLayerNoise(layer, x);
        
        // Höhe über dem Boden berechnen
        int heightAboveGround = Mathf.RoundToInt(
            Mathf.Lerp(layer.minHeightAboveGround, layer.maxHeightAboveGround, noise)
        );
        
        // Optional: Mit Ground-Noise interagieren
        if (groundGenerator != null && layer.noisePhaseOffset == 0)
        {
            // Synchron mit Ground - Plattformen folgen dem Terrain
            // (schon implizit durch groundHeight + heightAboveGround)
        }
        
        return heightAboveGround;
    }
    
    float GetLayerNoise(PlatformLayer layer, int x)
    {
        // Fractal Noise mit Layer-spezifischen Einstellungen
        float n = 0f;
        float freq = layer.noiseFrequency;
        float amp = 1f;
        float maxAmp = 0f;
        
        float seedOffset = layer.noiseSeed + layer.noisePhaseOffset;
        
        for (int i = 0; i < 2; i++)
        {
            n += Mathf.PerlinNoise((x + seedOffset) * freq, seedOffset * 0.3f) * amp;
            maxAmp += amp;
            freq *= 2f;
            amp *= 0.5f;
        }
        
        return n / maxAmp;
    }
    
    int GetGroundHeight(int x)
    {
        // Cache prüfen
        if (groundHeightCache.TryGetValue(x, out int cached))
            return cached;
        
        // Vom GroundGenerator holen oder Raycast
        int height = 3; // Default
        
        if (groundGenerator != null)
        {
            // Wir nutzen die Tilemap vom GroundGenerator
            Tilemap groundTilemap = groundGenerator.GetComponent<Tilemap>();
            if (groundTilemap != null)
            {
                // Von oben nach unten scannen bis wir ein Tile finden
                for (int y = 20; y >= 0; y--)
                {
                    if (groundTilemap.GetTile(new Vector3Int(x, y, 0)) != null)
                    {
                        height = y + 1; // Höhe über dem obersten Tile
                        break;
                    }
                }
            }
        }
        
        // Cachen (mit Limit um Speicher zu sparen)
        if (groundHeightCache.Count > 1000)
            groundHeightCache.Clear();
        
        groundHeightCache[x] = height;
        return height;
    }
    
    void RemoveChunk(int chunkIndex)
    {
        int startX = chunkIndex * chunkWidth;
        int maxY = 30; // Genug Höhe für Plattformen
        
        for (int x = startX; x < startX + chunkWidth; x++)
        {
            for (int y = 0; y < maxY; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
            }
            
            // Aus Cache entfernen
            groundHeightCache.Remove(x);
        }
    }
    
    // Editor: Default Layer hinzufügen
    void Reset()
    {
        if (layers.Count == 0)
        {
            layers.Add(new PlatformLayer()
            {
                name = "Lower Platforms",
                minHeightAboveGround = 3,
                maxHeightAboveGround = 5,
                noiseFrequency = 0.05f,
                spawnDensity = 0.5f
            });
            
            layers.Add(new PlatformLayer()
            {
                name = "Upper Platforms",
                minHeightAboveGround = 7,
                maxHeightAboveGround = 10,
                noiseFrequency = 0.08f,
                noisePhaseOffset = 50f,
                spawnDensity = 0.3f,
                minLength = 3,
                maxLength = 8
            });
        }
    }
    
    // Gizmos für Debug
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        // Visualisiere Layer-Höhen
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (!layer.enabled) continue;
            
            Gizmos.color = Color.HSVToRGB((float)i / layers.Count, 0.7f, 0.9f);
            
            if (player != null)
            {
                float y = GetGroundHeight(Mathf.RoundToInt(player.position.x)) + layer.lastHeight;
                Gizmos.DrawLine(
                    new Vector3(player.position.x - 5, y, 0),
                    new Vector3(player.position.x + 5, y, 0)
                );
            }
        }
    }
}
