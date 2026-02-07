using UnityEngine;
using UnityEngine.Tilemaps;
using WorldGeneration;

/// <summary>
/// Explosion - Zerstört Tiles und tötet Spieler im Radius.
/// Wiederverwendbar für Projektile, Bomben, etc.
/// </summary>
public class Explosion : MonoBehaviour
{
    [Header("Destruction")]
    [Tooltip("Radius der Tile-Zerstörung (in Tiles)")]
    public float tileDestructionRadius = 2f;
    
    [Header("Damage")]
    [Tooltip("Radius für Spieler-Schaden")]
    public float damageRadius = 1.5f;
    public LayerMask playerLayer;
    [Tooltip("Schaden den die Explosion verursacht (0 = kein Schaden)")]
    public int damage = 25;
    [Tooltip("Wenn true, tötet sofort unabhängig von HP")]
    public bool instantKill = false;
    
    [Header("Timing")]
    [Tooltip("Verzögerung bevor Explosion wirkt (für Animation)")]
    public float explosionDelay = 0f;
    [Tooltip("Lebenszeit des Objekts (für Animation)")]
    public float lifetime = 1f;
    
    [Header("Debug")]
    public bool debugMode = false;
    
    private bool hasExploded = false;
    
    void Start()
    {
        if (explosionDelay <= 0)
        {
            Explode();
        }
        else
        {
            Invoke(nameof(Explode), explosionDelay);
        }
        
        // Auto-Destroy nach Animation
        Destroy(gameObject, lifetime);
    }
    
    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        // Tiles zerstören
        if (tileDestructionRadius > 0)
        {
            DestroyTiles();
        }
        
        // Spieler Schaden
        if ((damage > 0 || instantKill) && damageRadius > 0)
        {
            DamagePlayer();
        }
    }
    
    void DestroyTiles()
    {
        ChunkManager chunkManager = ChunkManager.Instance;
        if (chunkManager == null) return;
        
        ChunkRenderer renderer = chunkManager.chunkRenderer;
        if (renderer == null || renderer.groundTilemap == null) return;
        
        Tilemap tilemap = renderer.groundTilemap;
        Vector3Int centerCell = tilemap.WorldToCell(transform.position);
        
        int radiusCeil = Mathf.CeilToInt(tileDestructionRadius);
        float radiusSq = tileDestructionRadius * tileDestructionRadius;
        int tilesDestroyed = 0;
        
        for (int dx = -radiusCeil; dx <= radiusCeil; dx++)
        {
            for (int dy = -radiusCeil; dy <= radiusCeil; dy++)
            {
                if (dx * dx + dy * dy <= radiusSq)
                {
                    Vector3Int tilePos = new Vector3Int(centerCell.x + dx, centerCell.y + dy, 0);
                    
                    if (tilemap.GetTile(tilePos) != null)
                    {
                        tilemap.SetTile(tilePos, null);
                        UpdateChunkData(chunkManager, tilePos.x, tilePos.y);
                        tilesDestroyed++;
                    }
                }
            }
        }
        
        // Collider aktualisieren
        renderer.RefreshColliders();
        
        if (debugMode)
        {
            Debug.Log($"[Explosion] Destroyed {tilesDestroyed} tiles at {transform.position}");
        }
    }
    
    void UpdateChunkData(ChunkManager chunkManager, int worldX, int worldY)
    {
        int chunkIndex = worldX / chunkManager.ChunkWidth;
        ChunkData chunk = chunkManager.GetChunk(chunkIndex);
        
        if (chunk != null)
        {
            int localX = worldX - (chunkIndex * chunkManager.ChunkWidth);
            if (chunk.IsInBounds(localX, worldY))
            {
                chunk[localX, worldY] = WorldGeneration.TileData.Air;
            }
        }
    }
    
    void DamagePlayer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius, playerLayer);
        
        foreach (var hit in hits)
        {
            PlayerManager player = hit.GetComponent<PlayerManager>();
            if (player == null)
                player = hit.GetComponentInParent<PlayerManager>();
            
            if (player != null)
            {
                if (instantKill)
                {
                    // Sofortiger Tod (wie KillZone)
                    player.Die();
                    if (debugMode)
                        Debug.Log($"[Explosion] Instant-killed player at {transform.position}");
                }
                else if (damage > 0)
                {
                    // Schaden zufügen
                    player.TakeDamage(damage);
                    if (debugMode)
                        Debug.Log($"[Explosion] Dealt {damage} damage to player at {transform.position}");
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Tile Destruction Radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, tileDestructionRadius * 0.5f); // Ungefähre Visualisierung
        
        // Damage Radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
