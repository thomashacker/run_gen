# World Generation System

## Übersicht

Das World Generation System ist ein **modulares, pass-basiertes System** für prozedurales Terrain-Generation in Unity 2D.

```
┌─────────────────────────────────────────────────────────┐
│                    ChunkManager                          │
│  (Orchestriert Generation, verwaltet Chunk-Lifecycle)   │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    GenerationContext                     │
│  (Shared State: Seed, CellSize, Neighbors, Utilities)   │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                   Pass Pipeline                          │
│  GroundPass → GapPass → PlatformPass → RampPass → ...   │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    ChunkRenderer                         │
│  (Konvertiert ChunkData zu Unity Tilemaps)              │
└─────────────────────────────────────────────────────────┘
```

---

## Core Komponenten

### TileData (`TileData.cs`)

Repräsentiert ein einzelnes Tile.

```csharp
// TileTypes
TileType.Air        // Leer
TileType.Solid      // Solides Tile
TileType.RampUp     // Rampe nach oben
TileType.RampDown   // Rampe nach unten
TileType.Gap        // Loch (spezieller Lufttyp)
TileType.Platform   // Platform-Tile

// TileLayers
TileLayer.Ground    // Boden-Layer
TileLayer.Platform  // Platform-Layer
TileLayer.Background

// Factory Methods
TileData.Air
TileData.Solid(layer)
TileData.Ramp(isUp, layer)
TileData.Gap

// Properties
tile.IsEmpty        // Air oder Gap
tile.IsSolid        // Solid oder Platform
tile.IsWalkable     // Solid, Platform oder Ramp
```

### ChunkData (`ChunkData.cs`)

Container für einen Chunk mit 2D Tile-Matrix.

```csharp
// Zugriff
chunk[localX, localY]           // Tile lesen/schreiben
chunk.IsInBounds(x, y)          // Bounds-Check

// Koordinaten
chunk.LocalToWorldX(localX)     // Lokal → World
chunk.WorldToLocalX(worldX)     // World → Lokal

// Queries
chunk.GetSurfaceHeight(localX)  // Erste solide Höhe von oben

// Bulk Operations
chunk.FillColumn(x, bottom, top, data)
chunk.FillRect(startX, startY, endX, endY, data)
chunk.Clear()

// Metadata
chunk.metadata.surfaceHeights[] // Ground-Höhen (von GroundPass gesetzt)
chunk.metadata.leftEdgeHeight   // Für nahtlose Übergänge
chunk.metadata.rightEdgeHeight
chunk.metadata.isComplete       // Generation abgeschlossen
```

### GenerationContext (`GenerationContext.cs`)

Shared State für alle Passes.

```csharp
// Settings
context.globalSeed
context.chunkWidth
context.chunkHeight
context.cellSize

// Nachbarn (für nahtlose Übergänge)
context.leftNeighbor
context.rightNeighbor

// Noise
context.GetNoise(x, frequency, seedOffset)
context.GetFractalNoise(x, frequency, octaves, seedOffset)

// Surface Queries
context.GetSurfaceHeightAt(worldX, currentChunk)

// Pass-Kommunikation
context.SetData<T>(key, value)
context.GetData<T>(key, defaultValue)
```

### ChunkUtilities (`ChunkUtilities.cs`)

Statische Utility-Methoden für häufige Operationen.

```csharp
// Surface Heights
ChunkUtilities.GetGroundHeight(chunk, localX)
ChunkUtilities.GetPlatformTops(chunk)
ChunkUtilities.GetCombinedSurfaceHeights(chunk, includeGround, includePlatforms)
ChunkUtilities.GetMaxGroundHeightInRange(chunk, localX, lookBehind, lookAhead)

// Nachbarn
ChunkUtilities.GetLeftNeighborHeight(context, fallback)
ChunkUtilities.GetRightNeighborHeight(context, fallback)

// Tile Checks
ChunkUtilities.IsEmpty(chunk, x, y)
ChunkUtilities.IsSolid(chunk, x, y)
ChunkUtilities.IsGap(chunk, x, y)
ChunkUtilities.HasFreeSpace(chunk, x, y, left, right, up)

// Ramp Detection
ChunkUtilities.IsRampAt(chunk, localX, localY)
ChunkUtilities.IsSurfaceRamp(chunk, localX, surfaceY)

// Position Occupation (Entity-Kollisionsprüfung)
ChunkUtilities.OccupyPosition(context, worldX, worldY)      // Position als besetzt markieren
ChunkUtilities.IsPositionOccupied(context, worldX, worldY)  // Prüfen ob besetzt
ChunkUtilities.IsAreaOccupied(context, worldX, worldY, radius)

// Position Calculation
ChunkUtilities.CalculateSpawnPosition(chunk, context, localX, surfaceY, heightAbove, center)
ChunkUtilities.TileToWorldPosition(tileX, tileY, cellSize)

// Noise
ChunkUtilities.GetNoiseHeight(context, worldX, frequency, min, max, octaves, seedOffset)
```

---

## Pass System

### Basis-Interface

```csharp
public interface IGeneratorPass
{
    string PassName { get; }
    bool Enabled { get; }
    ChunkData Execute(ChunkData chunk, GenerationContext context);
}
```

### Pass Erstellen

```csharp
using UnityEngine;

namespace WorldGeneration
{
    public class MyCustomPass : GeneratorPassBase
    {
        [Header("My Settings")]
        public float myParameter = 1f;
        
        public override void Initialize(GenerationContext context)
        {
            // Einmalig beim Start
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            // Chunk modifizieren
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                // ... Logik ...
                
                chunk[x, y] = TileData.Solid();
            }
            
            return chunk;
        }
        
        void Reset()
        {
            passName = "My Custom Pass";
        }
    }
}
```

---

## Existierende Passes

### GroundPass
- **Zweck**: Generiert Basis-Terrain mit Perlin Noise
- **Output**: Füllt Ground-Tiles, setzt `surfaceHeights` in Metadata
- **Parameter**: baseHeight, minHeight, maxHeight, noiseFrequency, plateauChance

### GapPass
- **Zweck**: Schneidet Löcher ins Terrain
- **Input**: Benötigt `surfaceHeights` von GroundPass
- **Parameter**: gapChance, minGapWidth, maxGapWidth, cooldown

### PlatformPass
- **Zweck**: Generiert schwebende Platforms
- **Input**: Benötigt `surfaceHeights` für Höhenberechnung
- **Parameter**: Platform-Layers mit eigenen Noise-Settings

### RampPass
- **Zweck**: Fügt Rampen bei Höhenunterschieden hinzu
- **Input**: Scannt Chunk nach Höhendifferenzen
- **Parameter**: rampChance, onlyForSingleSteps

### EnemyPass
- **Zweck**: Spawnt Gegner auf Oberflächen
- **Input**: Nutzt Ground + Platform Heights
- **Parameter**: Enemy-Liste mit SpawnChance, SpaceRequirements

### EmeraldPass
- **Zweck**: Spawnt Sammelobjekte (Münzen/Rubine)
- **Input**: Nutzt kombinierte Surface Heights
- **Parameter**: Emerald-Types, lineSpawnChance, minLength, maxLength

---

## Neuen Pass Erstellen - Checkliste

### 1. Script erstellen

```csharp
namespace WorldGeneration
{
    public class NewPass : GeneratorPassBase
    {
        // Parameter mit [Header] für Inspector
        [Header("Settings")]
        public float chance = 0.1f;
        public int safeStartColumns = 20;
        
        // Optional: Parent für gespawnte Objekte
        public Transform parent;
        
        public override void Initialize(GenerationContext context)
        {
            // Parent erstellen wenn nötig
            if (parent == null)
            {
                parent = new GameObject("MyObjects").transform;
            }
        }
        
        public override ChunkData Execute(ChunkData chunk, GenerationContext context)
        {
            // 1. Surface Heights holen (je nach Bedarf)
            int[] groundHeights = chunk.metadata.surfaceHeights;
            int[] platformTops = ChunkUtilities.GetPlatformTops(chunk);
            int[] combined = ChunkUtilities.GetCombinedSurfaceHeights(chunk);
            
            // 2. Über Chunk iterieren
            for (int x = 0; x < chunk.width; x++)
            {
                int worldX = chunk.LocalToWorldX(x);
                
                // Safe Start prüfen
                if (ChunkUtilities.IsInSafeStartZone(worldX, safeStartColumns))
                    continue;
                
                // Surface Height holen
                int surfaceY = combined[x];
                if (surfaceY < 0) continue; // Kein Boden
                
                // Chance prüfen
                if (Random.value > chance) continue;
                
                // 3. Aktion ausführen
                // Option A: Tiles modifizieren
                chunk[x, surfaceY + 1] = TileData.Solid(TileLayer.Ground);
                
                // Option B: Objekte spawnen
                Vector3 pos = ChunkUtilities.CalculateSpawnPosition(
                    chunk, context, x, surfaceY, 0.5f);
                // Instantiate(prefab, pos, ...);
            }
            
            return chunk;
        }
        
        void Reset()
        {
            passName = "New Pass";
        }
    }
}
```

### 2. Zum ChunkManager hinzufügen

1. Script als Child-Objekt des ChunkManager erstellen
2. Oder: Script auf eigenes GameObject und in `passes` Liste ziehen
3. **Reihenfolge beachten!** (siehe unten)

### 3. Pass-Reihenfolge

Passes werden in Reihenfolge der `passes` Liste ausgeführt:

```
1. GroundPass       ← MUSS zuerst (erstellt surfaceHeights)
2. GapPass          ← Modifiziert Ground
3. PlatformPass     ← Nutzt Ground-Heights für Abstand
4. RampPass         ← Muss nach Ground + Platform
5. EnemyPass        ← Nutzt finale Surfaces
6. EmeraldPass      ← Nutzt finale Surfaces
7. [Dein Pass]      ← Je nach Abhängigkeiten
```

---

## Häufige Patterns

### Surface Following (für Entities)

```csharp
int[] surfaces = ChunkUtilities.GetCombinedSurfaceHeights(chunk, true, true);

for (int x = 0; x < chunk.width; x++)
{
    int surfaceY = surfaces[x];
    if (surfaceY < 0) continue; // Gap
    
    Vector3 pos = ChunkUtilities.CalculateSpawnPosition(
        chunk, context, x, surfaceY, 0.5f, centerInTile: true);
    
    Instantiate(prefab, pos, Quaternion.identity, parent);
}
```

### Noise-basierte Höhe

```csharp
int height = ChunkUtilities.GetNoiseHeight(
    context, 
    worldX, 
    frequency: 0.05f, 
    minHeight: 5, 
    maxHeight: 15, 
    octaves: 3);
```

### Platz-Check für große Objekte

```csharp
bool hasSpace = ChunkUtilities.HasFreeSpace(
    chunk, x, y, 
    left: 2, 
    right: 2, 
    up: 3);
```

### Entity-Kollision vermeiden (Ramps + andere Entities)

```csharp
int surfaceY = surfaces[x];
int worldX = chunk.LocalToWorldX(x);

// Nicht auf Rampen spawnen
if (ChunkUtilities.IsSurfaceRamp(chunk, x, surfaceY))
    continue;

// Nicht wo schon ein Entity ist (Enemy, anderer Emerald, etc.)
if (ChunkUtilities.IsPositionOccupied(context, worldX, surfaceY + 1))
    continue;

// Spawnen...
Instantiate(prefab, pos, ...);

// Position als besetzt markieren für nachfolgende Passes
ChunkUtilities.OccupyPosition(context, worldX, surfaceY + 1);
```

### Nahtlose Übergänge

```csharp
int lastHeight = ChunkUtilities.GetLeftNeighborHeight(context, fallback: baseHeight);

for (int x = 0; x < chunk.width; x++)
{
    // Smooth transition from lastHeight
    int currentHeight = Mathf.Clamp(targetHeight, lastHeight - 1, lastHeight + 1);
    lastHeight = currentHeight;
}
```

---

## Debugging

### Gizmos

```csharp
void OnDrawGizmosSelected()
{
    // Zeige Bereiche im Scene View
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireCube(center, size);
}
```

### Debug Logs

```csharp
[Header("Debug")]
public bool debugMode = false;

// In Execute:
if (debugMode)
{
    Debug.Log($"[{passName}] Spawned at {worldX}");
}
```

---

## Tips

1. **Immer `chunk.IsInBounds()` prüfen** bevor du Tiles liest/schreibst
2. **`safeStartColumns`** verhindert Spawns am Spielstart
3. **ChunkUtilities nutzen** statt Code zu duplizieren
4. **Metadata setzen** wenn andere Passes deine Daten brauchen
5. **Pass-Reihenfolge** ist kritisch - Ground muss zuerst!
