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
    
    [Header("Knockback")]
    [Tooltip("Aktiviert Knockback-Effekt")]
    public bool enableKnockback = true;
    [Tooltip("Radius für Knockback (0 = nutzt damageRadius)")]
    public float knockbackRadius = 0f;
    [Tooltip("Stärke des Knockbacks")]
    public float knockbackForce = 15f;
    [Tooltip("Vertikale Komponente des Knockbacks (0-1, höher = mehr nach oben)")]
    [Range(0f, 1f)]
    public float knockbackUpwardBias = 0.3f;
    
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
        
        // Knockback
        if (enableKnockback && knockbackForce > 0)
        {
            ApplyKnockback();
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
    
    void ApplyKnockback()
    {
        float radius = knockbackRadius > 0 ? knockbackRadius : damageRadius;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, playerLayer);
        
        foreach (var hit in hits)
        {
            Rigidbody2D playerRb = hit.GetComponent<Rigidbody2D>();
            if (playerRb == null)
                playerRb = hit.GetComponentInParent<Rigidbody2D>();
            
            if (playerRb != null)
            {
                // Richtung weg von der Explosion
                Vector2 direction = (playerRb.position - (Vector2)transform.position).normalized;
                
                // Falls direkt auf der Explosion, nach oben werfen
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector2.up;
                
                // Upward Bias hinzufügen (macht den Knockback "explosiver")
                direction = new Vector2(
                    direction.x * (1f - knockbackUpwardBias),
                    direction.y + knockbackUpwardBias
                ).normalized;
                
                // Distanz-basierte Abschwächung (näher = stärker)
                float distance = Vector2.Distance(transform.position, playerRb.position);
                float falloff = 1f - Mathf.Clamp01(distance / radius);
                float finalForce = knockbackForce * falloff;
                
                // Kraft anwenden - Velocity direkt setzen für sofortigen Effekt
                playerRb.linearVelocity = direction * finalForce;
                
                if (debugMode)
                {
                    Debug.Log($"[Explosion] Knockback applied: dir={direction}, force={finalForce:F1}");
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Tile Destruction Radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, tileDestructionRadius * 0.5f);
        
        // Damage Radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
        
        // Knockback Radius (wenn unterschiedlich zum Damage Radius)
        if (enableKnockback && knockbackRadius > 0 && knockbackRadius != damageRadius)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, knockbackRadius);
        }
    }
}
