using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DragonEnemy : MonoBehaviour
{
    public enum DragonState
    {
        Idle,       // Wartet auf Spieler
        Aim,        // Zielen auf Spieler/letzte Position
        Attack      // Schuss abfeuern
    }
    
    [Header("Current State")]
    [SerializeField] private DragonState currentState = DragonState.Idle;
    
    [Header("Health")]
    public int maxHealth = 3;
    [SerializeField] private int currentHealth;
    
    // Events
    public event Action OnDamaged;
    public event Action OnDeath;
    
    [Header("Detection")]
    [Tooltip("Radius in dem der Spieler erkannt wird")]
    public float detectionRadius = 10f;
    public LayerMask playerLayer;
    public LayerMask groundLayer;
    
    [Header("Attack")]
    [Tooltip("Zeit die der Drache zielt bevor er schießt")]
    public float aimDuration = 2f;
    [Tooltip("Abklingzeit nach Schuss")]
    public float attackCooldown = 1.5f;
    public GameObject projectilePrefab;
    public float projectileSpeed = 8f;
    public Transform firePoint;
    
    [Header("Aim Laser")]
    public LineRenderer aimLaser;
    public float laserMaxLength = 20f;
    public LayerMask laserHitLayers;
    
    [Header("References")]
    public SpriteRenderer spriteRenderer;
    
    [Header("Death")]
    [Tooltip("Prefab das beim Tod gespawnt wird (z.B. Emerald)")]
    public GameObject deathDropPrefab;
    [Tooltip("Offset für den Drop-Spawn")]
    public Vector3 deathDropOffset = Vector3.zero;
    [Tooltip("Effekt beim Tod (Particles, etc.)")]
    public GameObject deathEffectPrefab;
    [Tooltip("Dauer der Death-Animation (danach wird zerstört)")]
    public float deathAnimationDuration = 0.5f;
    
    // Private
    private Rigidbody2D rb;
    private Transform player;
    private float aimTimer;
    private float cooldownTimer;
    private Vector2 aimDirection;
    private Vector2 lastKnownPlayerPosition; // Letzte Position wo Spieler sichtbar war
    private bool hasLastKnownPosition = false;
    private bool isDead = false;
    
    // Public Access
    public DragonState CurrentState => currentState;
    public int CurrentHealth => currentHealth;
    public bool IsPlayerInRange => player != null && Vector2.Distance(transform.position, player.position) <= detectionRadius;
    public bool IsOnCooldown => cooldownTimer > 0f;
    public bool IsDead => isDead;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 2f;
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (aimLaser != null)
            aimLaser.enabled = false;
        
        // Health initialisieren
        currentHealth = maxHealth;
    }
    
    void Start()
    {
        // Spieler finden
        PlayerManager pm = FindAnyObjectByType<PlayerManager>();
        if (pm != null)
            player = pm.transform;
    }
    
    void Update()
    {
        if (player == null || isDead) return;
        
        UpdateState();
        UpdateAimLaser();
        UpdateSpriteFlip();
    }
    
    void FixedUpdate()
    {
        // Keine Bewegung - nur Gravitation
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }
    
    void UpdateState()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        bool canSeePlayer = CanSeePlayer();
        
        // Wenn wir den Spieler sehen, Position speichern
        if (canSeePlayer)
        {
            lastKnownPlayerPosition = player.position;
            hasLastKnownPosition = true;
        }
        
        // Cooldown Timer
        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;
        
        switch (currentState)
        {
            case DragonState.Idle:
                // Spieler erkannt und in Reichweite?
                if (distanceToPlayer <= detectionRadius && canSeePlayer && cooldownTimer <= 0)
                {
                    StartAiming();
                }
                break;
                
            case DragonState.Aim:
                // Ziel-Richtung updaten
                Vector2 targetPosition;
                
                if (canSeePlayer)
                {
                    // Spieler sichtbar → direkt auf ihn zielen
                    targetPosition = player.position;
                }
                else if (hasLastKnownPosition)
                {
                    // Spieler nicht sichtbar → auf letzte bekannte Position zielen
                    targetPosition = lastKnownPlayerPosition;
                }
                else
                {
                    // Kein Ziel → zurück zu Idle
                    StopAiming();
                    break;
                }
                
                // Ziel-Richtung mit Delay (Trägheit)
                Vector2 targetDir = (targetPosition - (Vector2)transform.position).normalized;
                aimDirection = Vector2.Lerp(aimDirection, targetDir, Time.deltaTime * 3f);
                
                aimTimer -= Time.deltaTime;
                if (aimTimer <= 0)
                {
                    currentState = DragonState.Attack;
                    FireProjectile();
                }
                break;
                
            case DragonState.Attack:
                // Zurück zu Idle nach Schuss
                StopAiming();
                cooldownTimer = attackCooldown;
                currentState = DragonState.Idle;
                
                // Letzte Position zurücksetzen nach Schuss
                hasLastKnownPosition = false;
                break;
        }
    }
    
    void StartAiming()
    {
        currentState = DragonState.Aim;
        aimTimer = aimDuration;
        
        // Start-Richtung auf Spieler setzen
        aimDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        
        if (aimLaser != null) 
            aimLaser.enabled = true;
    }
    
    void StopAiming()
    {
        if (aimLaser != null) 
            aimLaser.enabled = false;
    }
    
    bool CanSeePlayer()
    {
        if (player == null) return false;
        
        Vector2 direction = (player.position - transform.position).normalized;
        float distance = Vector2.Distance(transform.position, player.position);
        
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, groundLayer);
        
        // Wenn wir Ground treffen bevor wir den Spieler erreichen, können wir ihn nicht sehen
        return hit.collider == null;
    }
    
    void UpdateAimLaser()
    {
        if (aimLaser == null || !aimLaser.enabled) return;
        
        Vector2 start = firePoint != null ? firePoint.position : transform.position;
        Vector2 direction = aimDirection.normalized;
        
        RaycastHit2D hit = Physics2D.Raycast(start, direction, laserMaxLength, laserHitLayers);
        
        Vector2 end = hit.collider != null 
            ? hit.point 
            : start + direction * laserMaxLength;
        
        aimLaser.SetPosition(0, start);
        aimLaser.SetPosition(1, end);
    }
    
    void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        
        // Basierend auf Zielrichtung
        if (currentState == DragonState.Aim && Mathf.Abs(aimDirection.x) > 0.1f)
        {
            spriteRenderer.flipX = aimDirection.x < 0;
        }
        // Oder Richtung zum Spieler wenn Idle
        else if (player != null)
        {
            float dirToPlayer = player.position.x - transform.position.x;
            if (Mathf.Abs(dirToPlayer) > 0.1f)
            {
                spriteRenderer.flipX = dirToPlayer < 0;
            }
        }
    }
    
    void FireProjectile()
    {
        if (projectilePrefab == null) return;
        
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        
        GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        
        DragonProjectile proj = projectile.GetComponent<DragonProjectile>();
        if (proj != null)
        {
            proj.Initialize(aimDirection.normalized, projectileSpeed, gameObject);
        }
        else
        {
            // Fallback: Einfach Rigidbody Velocity setzen
            Rigidbody2D projRb = projectile.GetComponent<Rigidbody2D>();
            if (projRb != null)
            {
                projRb.linearVelocity = aimDirection.normalized * projectileSpeed;
            }
        }
    }
    
    /// <summary>
    /// Wird aufgerufen wenn der Spieler auf den Drachen springt (Instakill)
    /// </summary>
    public void OnStomped()
    {
        Die();
    }
    
    /// <summary>
    /// Bricht den aktuellen Angriff ab und startet Cooldown
    /// </summary>
    public void CancelAttack()
    {
        if (currentState == DragonState.Aim || currentState == DragonState.Attack)
        {
            StopAiming();
            cooldownTimer = attackCooldown;
            currentState = DragonState.Idle;
            hasLastKnownPosition = false;
        }
    }
    
    /// <summary>
    /// Fügt dem Dragon Schaden zu
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (damage <= 0) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        
        OnDamaged?.Invoke();
        
        Debug.Log($"[Dragon] Took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    /// <summary>
    /// Dragon stirbt
    /// </summary>
    void Die()
    {
        if (isDead) return;
        isDead = true;
        
        // Angriff abbrechen
        StopAiming();
        
        // Alle Collider deaktivieren
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }
        
        // Rigidbody stoppen
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        
        // Event für Animator
        OnDeath?.Invoke();
        
        // Drop spawnen (z.B. Emerald)
        if (deathDropPrefab != null)
        {
            Instantiate(deathDropPrefab, transform.position + deathDropOffset, Quaternion.identity);
        }
        
        // Death Effect spawnen (Particles, etc.)
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Verzögert zerstören (nach Death Animation)
        Destroy(gameObject, deathAnimationDuration);
    }
    
    void OnDrawGizmosSelected()
    {
        // Detection Radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Last Known Position
        if (hasLastKnownPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(lastKnownPlayerPosition, 0.3f);
            Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);
        }
    }
}
