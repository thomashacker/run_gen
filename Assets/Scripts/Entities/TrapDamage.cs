using UnityEngine;

/// <summary>
/// Fügt dem Spieler Schaden zu wenn er den Trigger berührt.
/// Konfigurierbar: 1 Herz Schaden oder Soforttod.
/// 
/// Reagiert auf beide Kollisionsarten (Trigger und Collision),
/// sodass nur ein einzelner PolygonCollider2D benötigt wird.
///
/// Prefab-Setup:
///   - SpriteRenderer (Sprite)
///   - PolygonCollider2D (isTrigger = true ODER false, beides funktioniert)
///   - TrapDamage (dieses Script)
/// </summary>
public class TrapDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Sofortiger Tod bei Berührung (wie KillZone)")]
    public bool instantKill = false;
    
    [Tooltip("Schaden in Herzen (wird ignoriert wenn instantKill aktiv)")]
    [Min(1)]
    public int damage = 1;
    
    [Header("Detection")]
    [Tooltip("Layer des Spielers für Erkennung")]
    public LayerMask playerLayer;
    
    [Header("Debug")]
    public bool debugMode = false;
    
    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamage(collision.gameObject);
    }
    
    void TryDamage(GameObject other)
    {
        // Nur auf Player-Layer reagieren
        if (playerLayer != 0 && ((1 << other.layer) & playerLayer) == 0)
            return;
        
        PlayerManager player = other.GetComponent<PlayerManager>();
        if (player == null)
            player = other.GetComponentInParent<PlayerManager>();
        
        if (player == null) return;
        
        if (instantKill)
        {
            player.Die();
            if (debugMode)
                Debug.Log($"[TrapDamage] Instant-killed player at {transform.position}");
        }
        else
        {
            player.TakeDamage(damage);
            if (debugMode)
                Debug.Log($"[TrapDamage] Dealt {damage} damage to player at {transform.position}");
        }
    }
}
