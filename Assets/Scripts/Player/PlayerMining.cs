using UnityEngine;
using UnityEngine.Tilemaps;
using WorldGeneration;

/// <summary>
/// Ermöglicht dem Spieler, Tiles in alle Richtungen abzubauen.
/// Richtung wird durch WASD bestimmt (gehalten) + E zum Abbauen.
/// Ohne Richtungstaste wird die Blickrichtung (links/rechts) verwendet.
/// 
/// Nutzt direkte Grid-Abfrage (kein Raycast) — Community-Standard für Tilemaps
/// mit CompositeCollider2D, da Raycasts dort unzuverlässig sind.
/// </summary>
public class PlayerMining : MonoBehaviour
{
    [Header("Mining Settings")]
    [Tooltip("Taste zum Abbauen")]
    public KeyCode mineKey = KeyCode.E;
    [Tooltip("Schaden pro Treffer")]
    public int damagePerHit = 1;
    
    [Header("Grid Alignment")]
    [Tooltip("Manueller Offset um die Mining-Position an den Spieler-Sprite anzupassen")]
    public Vector2 gridOffset = new Vector2(-0.01f, -0.01f);
    
    [Header("Direction Keys")]
    public KeyCode keyRight = KeyCode.D;
    public KeyCode keyLeft = KeyCode.A;
    public KeyCode keyUp = KeyCode.W;
    public KeyCode keyDown = KeyCode.S;
    
    [Header("References (auto-filled)")]
    public PlayerAnimator playerAnimator;
    public Collider2D playerCollider;
    
    [Header("Debug")]
    public bool debugMode = false;
    
    void Awake()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponent<PlayerAnimator>();
        
        if (playerCollider == null)
            playerCollider = GetComponent<Collider2D>();
    }
    
    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            return;
        
        if (Input.GetKeyDown(mineKey))
        {
            TryMine();
        }
    }
    
    /// <summary>
    /// Bestimmt die Mining-Richtung als Tile-Offset.
    /// </summary>
    Vector2Int GetMineOffset()
    {
        bool right = Input.GetKey(keyRight);
        bool left = Input.GetKey(keyLeft);
        bool up = Input.GetKey(keyUp);
        bool down = Input.GetKey(keyDown);
        
        if (right || left || up || down)
        {
            int dx = 0, dy = 0;
            if (right) dx += 1;
            if (left) dx -= 1;
            if (up) dy += 1;
            if (down) dy -= 1;
            
            // Bei diagonalem Input: nur eine Achse
            if (dx != 0 && dy != 0)
            {
                if (Mathf.Abs(dy) > Mathf.Abs(dx))
                    dx = 0;
                else
                    dy = 0;
            }
            
            return new Vector2Int(dx, dy);
        }
        
        // Fallback: Blickrichtung
        int facing = (playerAnimator != null && !playerAnimator.IsFacingRight) ? -1 : 1;
        return new Vector2Int(facing, 0);
    }
    
    void TryMine()
    {
        TileHealthManager healthManager = TileHealthManager.Instance;
        if (healthManager == null) return;
        
        ChunkManager chunkManager = ChunkManager.Instance;
        if (chunkManager == null || chunkManager.chunkRenderer == null) return;
        
        Tilemap tilemap = chunkManager.chunkRenderer.groundTilemap;
        if (tilemap == null) return;
        
        Vector2Int offset = GetMineOffset();
        
        // Spieler-Zelle bestimmen:
        // transform.position = Sprite-Pivot (typisch: Fuß-Mitte)
        // gridOffset = manueller Offset um das Mining-Gitter am Spieler auszurichten
        // Tweak gridOffset im Inspector bis es sich richtig anfühlt
        Vector3 refPos = transform.position + (Vector3)gridOffset;
        Vector3Int playerCell = tilemap.WorldToCell(refPos);
        
        // Ziel-Zelle = Spieler-Zelle + Richtung
        Vector3Int targetCell = new Vector3Int(
            playerCell.x + offset.x,
            playerCell.y + offset.y,
            0
        );
        
        if (debugMode)
        {
            Debug.Log($"[PlayerMining] transform: {transform.position}, refPos: {refPos}, " +
                $"playerCell: {playerCell}, offset: {offset}, target: {targetCell}");
        }
        
        // Nur das exakte Ziel prüfen — kein diagonales Fallback
        Vector3Int? found = FindTileAt(tilemap, chunkManager, targetCell);
        
        if (!found.HasValue)
        {
            if (debugMode) Debug.Log($"[PlayerMining] No tile at {targetCell}");
            return;
        }
        
        if (debugMode)
            Debug.Log($"[PlayerMining] Mining tile at ({targetCell.x}, {targetCell.y})");
        
        healthManager.DamageTile(targetCell.x, targetCell.y, damagePerHit);
    }
    
    /// <summary>
    /// Prüft ob an einer Cell-Position ein Tile existiert (Ground oder Platform).
    /// Gibt die Position zurück wenn gefunden, sonst null.
    /// </summary>
    Vector3Int? FindTileAt(Tilemap groundTilemap, ChunkManager chunkManager, Vector3Int cell)
    {
        if (groundTilemap.GetTile(cell) != null)
            return cell;
        
        if (chunkManager.chunkRenderer.platformTilemap != null
            && chunkManager.chunkRenderer.platformTilemap != groundTilemap
            && chunkManager.chunkRenderer.platformTilemap.GetTile(cell) != null)
            return cell;
        
        return null;
    }
    
    void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        
        if (!Application.isPlaying) return;
        
        ChunkManager chunkManager = ChunkManager.Instance;
        if (chunkManager == null || chunkManager.chunkRenderer == null) return;
        Tilemap tilemap = chunkManager.chunkRenderer.groundTilemap;
        if (tilemap == null) return;
        
        Vector3 refPos = transform.position + (Vector3)gridOffset;
        Vector3Int playerCell = tilemap.WorldToCell(refPos);
        Vector3 cellSize = tilemap.cellSize;
        
        // Referenzpunkt anzeigen (wo das System denkt der Spieler ist)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(refPos, 0.1f);
        
        // Spieler-Zelle hervorheben
        Vector3 playerCellCenter = tilemap.GetCellCenterWorld(playerCell);
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(playerCellCenter, cellSize * 0.95f);
        
        // Umliegende Zellen anzeigen
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                
                Vector3Int cell = new Vector3Int(playerCell.x + dx, playerCell.y + dy, 0);
                Vector3 worldCenter = tilemap.GetCellCenterWorld(cell);
                bool hasTile = tilemap.GetTile(cell) != null;
                
                Gizmos.color = hasTile ? new Color(1f, 0.5f, 0f, 0.3f) : new Color(1f, 1f, 1f, 0.1f);
                Gizmos.DrawWireCube(worldCenter, cellSize * 0.9f);
            }
        }
        
        // Aktives Ziel hervorheben
        Vector2Int offset = GetMineOffset();
        Vector3Int target = new Vector3Int(playerCell.x + offset.x, playerCell.y + offset.y, 0);
        Vector3 targetWorld = tilemap.GetCellCenterWorld(target);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(targetWorld, cellSize);
    }
}
