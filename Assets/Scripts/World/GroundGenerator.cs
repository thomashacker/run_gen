using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class GroundGenerator : MonoBehaviour
{
    [Header("Tile Setup")]
    public TileBase groundTile;
    
    [Header("Ramps (Optional)")]
    public bool useRamps = true;
    public TileBase rampUpTile;              // Rampe aufwärts (von links-unten nach rechts-oben)
    public TileBase rampDownTile;            // Rampe abwärts (von links-oben nach rechts-unten)
    
    [Header("Chunk Settings")]
    public int chunkWidth = 20;
    public int chunksAhead = 3;              // Chunks vor dem Spieler generieren
    public int chunksBehind = 1;             // Chunks hinter dem Spieler behalten
    public int startOffsetColumns = 10;      // Spalten links vom Ursprung die vorher generiert werden
    
    [Header("Terrain Height - Noise Based")]
    public int baseHeight = 3;               // Basis-Höhe
    public int minHeight = 2;                // Minimale Höhe
    public int maxHeight = 10;               // Maximale Höhe
    [Range(0.01f, 0.2f)]
    public float noiseFrequency = 0.05f;     // Kleiner = längere Wellen
    public float noiseAmplitude = 5f;        // Stärke der Variation
    
    [Header("Terrain Control")]
    [Range(1, 3)]
    public int maxSlopePerColumn = 1;        // Max Höhenänderung pro Spalte (1 = fair)
    [Range(0f, 0.5f)]
    public float plateauChance = 0.3f;       // Chance für flache Strecken
    public int minPlateauLength = 3;
    public int maxPlateauLength = 10;
    
    [Header("Gaps / Holes")]
    [Range(0f, 0.15f)]
    public float gapChance = 0.08f;
    public int minGapWidth = 2;
    public int maxGapWidth = 4;
    public int minGapCooldown = 10;          // Mindestabstand zwischen Gaps
    public int safeStartColumns = 15;
    
    [Header("References")]
    public Transform player;                 // Spieler-Transform
    
    // Singleton
    public static GroundGenerator Instance { get; private set; }
    
    // Public Properties für andere Scripts
    public int CurrentHeight => lastHeight;
    public float CellSizeY => grid != null ? grid.cellSize.y : 1f;
    public float CellSizeX => cellSizeX;
    
    // Private
    private Tilemap tilemap;
    private Grid grid;
    private float cellSizeX;
    private int leftmostChunkIndex = 0;
    private int rightmostChunkIndex = -1;
    
    // Terrain State
    private float noiseSeed;
    private int lastHeight;
    private int previousHeight;              // Höhe der vorherigen Spalte (für Rampen)
    private int plateauRemaining = 0;
    private int totalColumnsGenerated = 0;
    
    // Gap State
    private int gapRemaining = 0;
    private int gapCooldown = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        
        tilemap = GetComponent<Tilemap>();
        grid = GetComponentInParent<Grid>();
        cellSizeX = grid != null ? grid.cellSize.x : 1f;
        
        // Random seed für Noise
        noiseSeed = Random.Range(0f, 10000f);
        lastHeight = baseHeight;
        previousHeight = baseHeight;
    }
    
    void Start()
    {
        // Spieler automatisch finden falls nicht zugewiesen
        if (player == null)
        {
            PlayerManager pm = FindAnyObjectByType<PlayerManager>();
            if (pm != null)
            {
                player = pm.transform;
            }
        }
        
        // Offset-Spalten links vom Ursprung generieren (flacher Boden)
        for (int x = -startOffsetColumns; x < 0; x++)
        {
            // Flache Tiles auf baseHeight für sicheren Start
            for (int y = 0; y < baseHeight; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
        }
        previousHeight = baseHeight;
        
        // Initiale Chunks basierend auf Spielerposition generieren
        UpdateChunks();
    }
    
    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            return;
        
        if (player == null) return;
        
        // Chunk Management basierend auf Spielerposition
        UpdateChunks();
    }
    
    void UpdateChunks()
    {
        if (player == null) return;
        
        // Berechne in welchem Chunk der Spieler ist
        int playerChunk = Mathf.FloorToInt(player.position.x / (chunkWidth * cellSizeX));
        
        // Chunks vor dem Spieler generieren
        int targetRightChunk = playerChunk + chunksAhead;
        while (rightmostChunkIndex < targetRightChunk)
        {
            rightmostChunkIndex++;
            GenerateChunk(rightmostChunkIndex);
        }
        
        // Chunks hinter dem Spieler entfernen (basierend auf KillZone oder Spieler)
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
        totalColumnsGenerated++;
        
        // Gap Cooldown
        if (gapCooldown > 0) gapCooldown--;
        
        // === GAP HANDLING ===
        if (gapRemaining > 0)
        {
            gapRemaining--;
            previousHeight = lastHeight; // Höhe merken auch bei Gaps
            return; // Kein Boden - es ist ein Loch!
        }
        
        // Neuen Gap starten?
        if (CanStartGap())
        {
            gapRemaining = Random.Range(minGapWidth, maxGapWidth + 1) - 1;
            gapCooldown = minGapCooldown;
            previousHeight = lastHeight;
            return;
        }
        
        // === HÖHE BERECHNEN ===
        int height = GetHeight(x);
        int heightDiff = height - previousHeight;
        
        // === TILES SETZEN ===
        // Prüfen ob wir Rampen verwenden können
        bool canUseRamps = useRamps && rampUpTile != null && rampDownTile != null;
        bool useRampHere = canUseRamps && Mathf.Abs(heightDiff) == 1;
        
        if (useRampHere && heightDiff == 1)
        {
            // AUFWÄRTS: Terrain geht hoch
            // Normale Tiles bis previousHeight
            for (int y = 0; y < previousHeight; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
            
            // Aufwärts-Rampe an Position previousHeight
            tilemap.SetTile(new Vector3Int(x, previousHeight, 0), rampUpTile);
        }
        else if (useRampHere && heightDiff == -1)
        {
            // ABWÄRTS: Terrain geht runter
            // Normale Tiles bis height (neue niedrigere Höhe)
            for (int y = 0; y < height; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
            
            // Abwärts-Rampe an Position height
            tilemap.SetTile(new Vector3Int(x, height, 0), rampDownTile);
        }
        else
        {
            // Keine Rampe - normale Tiles
            for (int y = 0; y < height; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
        }
        
        // Höhe für nächste Spalte merken
        previousHeight = height;
    }

    int GetHeight(int x)
    {
        // Plateau aktiv? Höhe halten
        if (plateauRemaining > 0)
        {
            plateauRemaining--;
            return lastHeight;
        }
        
        // Noise-basierte Höhe
        float noise = GetFractalNoise(x);
        float rawHeight = Mathf.Lerp(minHeight, maxHeight, noise);
        int targetHeight = Mathf.RoundToInt(rawHeight);
        
        // Slope begrenzen (macht es fair/spielbar)
        targetHeight = Mathf.Clamp(targetHeight, lastHeight - maxSlopePerColumn, lastHeight + maxSlopePerColumn);
        
        // Clamp auf min/max
        targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);
        
        // Plateau starten?
        if (Random.value < plateauChance)
        {
            plateauRemaining = Random.Range(minPlateauLength, maxPlateauLength + 1);
        }
        
        lastHeight = targetHeight;
        return lastHeight;
    }

    float GetFractalNoise(int x)
    {
        // Mehrere Noise-Oktaven für natürlichere Variation
        float n = 0f;
        float freq = noiseFrequency;
        float amp = 1f;
        float maxAmp = 0f;
        
        // 3 Oktaven
        for (int i = 0; i < 3; i++)
        {
            n += Mathf.PerlinNoise((x + noiseSeed) * freq, noiseSeed * 0.5f) * amp;
            maxAmp += amp;
            freq *= 2f;      // Frequenz verdoppeln
            amp *= 0.5f;     // Amplitude halbieren
        }
        
        return n / maxAmp; // Normalisieren auf 0-1
    }

    bool CanStartGap()
    {
        if (totalColumnsGenerated <= safeStartColumns) return false;
        if (gapCooldown > 0) return false;
        return Random.value < gapChance;
    }

    void RemoveChunk(int chunkIndex)
    {
        int startX = chunkIndex * chunkWidth;
        int maxY = maxHeight + 5;
        
        for (int x = startX; x < startX + chunkWidth; x++)
        {
            for (int y = -5; y < maxY; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
    }
}
