using UnityEngine;

/// <summary>
/// Projektil vom Dragon Enemy.
/// Fliegt in eine Richtung und spawnt Explosion bei Treffer.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DragonProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float lifetime = 5f;
    public LayerMask groundLayer;
    public LayerMask playerLayer;
    [Tooltip("One-way platforms (e.g. Platform Effector tilemap) – projectile explodes on contact")]
    public LayerMask platformLayer;
    [Tooltip("Other projectiles (same layer as this) – projectile explodes when hitting another projectile")]
    public LayerMask projectileLayer;
    
    [Header("Explosion")]
    [Tooltip("Explosion Prefab das bei Treffer gespawnt wird")]
    public GameObject explosionPrefab;
    
    private Rigidbody2D rb;
    private bool hasHit = false;
    private GameObject owner;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }
    
    void Start()
    {
        Destroy(gameObject, lifetime);
    }
    
    public void Initialize(Vector2 direction, float speed, GameObject owner = null)
    {
        this.owner = owner;
        
        rb.linearVelocity = direction.normalized * speed;
        
        // Rotation basierend auf Richtung
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (IsOwner(other.gameObject)) return;
        if (!IsValidTarget(other.gameObject)) return;
        
        HandleHit(other.ClosestPoint(transform.position));
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHit) return;
        if (IsOwner(collision.gameObject)) return;
        if (!IsValidTarget(collision.gameObject)) return;
        
        Vector2 hitPoint;
        if (collision.contacts.Length > 0)
        {
            // Hit-Point leicht in Richtung der Kollision verschieben
            Vector2 contactPoint = collision.contacts[0].point;
            Vector2 normal = collision.contacts[0].normal;
            hitPoint = contactPoint - normal * 0.25f;
        }
        else
        {
            hitPoint = transform.position;
        }
        
        HandleHit(hitPoint);
    }
    
    bool IsOwner(GameObject obj)
    {
        return owner != null && (obj == owner || obj.transform.IsChildOf(owner.transform));
    }
    
    bool IsValidTarget(GameObject obj)
    {
        int objLayer = obj.layer;

        bool isGround = ((1 << objLayer) & groundLayer) != 0;
        bool isPlayer = ((1 << objLayer) & playerLayer) != 0;
        bool isProjectile = ((1 << objLayer) & projectileLayer) != 0;
        bool isOtherDragonProjectile = obj.GetComponent<DragonProjectile>() != null;

        // Platforms are intentionally ignored — projectiles pass through them
        return isGround || isPlayer || isProjectile || isOtherDragonProjectile;
    }
    
    void HandleHit(Vector2 hitPoint)
    {
        hasHit = true;
        
        // Explosion spawnen
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, hitPoint, Quaternion.identity);
        }
        
        // Projektil zerstören
        Destroy(gameObject);
    }
}
