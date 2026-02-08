using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerManager : MonoBehaviour
{
    [Header("Ground Movement (AddForce)")]
    public float moveSpeed = 8f;
    [Tooltip("Kraft-Gain: force = (targetSpeed - vx) * acceleration. Kleiner = weicher, größer = schneller. Stabil wenn < 1/Time.fixedDeltaTime (~50).")]
    public float acceleration = 25f;
    public float deceleration = 25f;

    [Header("Jump")]
    public float jumpForce = 13f;
    [Tooltip("Gravity while rising AND jump held = höherer Sprung. Nach Loslassen = stärkerer Fall (kurzer Tipp = niedrig).")]
    public float gravityRiseHold = 0.4f;
    public float gravityFall = 1.8f;
    [Tooltip("Noch stärkere Gravity direkt beim Loslassen für knackigen Kurzsprung.")]
    public float jumpCutGravityMult = 1.5f;
    public float maxFallSpeed = 22f;
    [Tooltip("Sekunden nach Verlassen des Bodens, in der man noch springen kann.")]
    public float coyoteTime = 0.1f;
    [Tooltip("Sprung wird ausgeführt, wenn man innerhalb dieser Sekunden landet (früh drücken).")]
    public float jumpBufferTime = 0.15f;
    [Tooltip("Am Sprung-Scheitelpunkt: weniger Gravity + bessere Kontrolle.")]
    public float jumpHangThreshold = 0.5f;
    public float jumpHangGravityMult = 0.5f;
    [Tooltip("Luft-Beschleunigung am Scheitelpunkt (z.B. 1.2 = 20% mehr).")]
    public float jumpHangAccelMult = 1.2f;
    [Tooltip("Halten Runter in der Luft = schneller fallen.")]
    public float fastFallGravityMult = 1.4f;

    [Header("Air Control (AddForce)")]
    public float airAcceleration = 20f;
    public float airDeceleration = 20f;

    [Header("Wall Jump")]
    [Tooltip("Aktiviert Wall Jump")]
    public bool enableWallJump = true;
    [Tooltip("Vertikale Kraft beim Wall Jump")]
    public float wallJumpForceY = 14f;
    [Tooltip("Horizontale Kraft weg von der Wand")]
    public float wallJumpForceX = 12f;
    [Tooltip("Zeit nach Wall Jump in der Input reduziert ist (verhindert sofortiges Zurück zur Wand)")]
    public float wallJumpInputLockTime = 0.15f;
    [Tooltip("Coyote Time für Wall Jump (Zeit nach Verlassen der Wand)")]
    public float wallCoyoteTime = 0.1f;
    
    [Header("Wall Slide (Optional)")]
    [Tooltip("Aktiviert langsameres Fallen an der Wand")]
    public bool enableWallSlide = true;
    [Tooltip("Maximale Fallgeschwindigkeit an der Wand")]
    public float wallSlideMaxSpeed = 3f;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    [Tooltip("Layer für Enemies auf denen man stehen/springen kann")]
    public LayerMask enemyLayer;
    [Tooltip("Breite des Ground-Check Box (sollte etwas kleiner als Spieler sein)")]
    public float groundCheckWidth = 0.4f;
    [Tooltip("Wie weit nach unten gecheckt wird")]
    public float groundCheckDistance = 0.1f;
    public Transform groundCheck;

    [Header("Wall Check (optional)")]
    public float wallCheckDistance = 0.1f;
    
    [Header("Hearts")]
    [Tooltip("Startanzahl der Herzen")]
    public int maxHearts = 3;
    public float invincibilityDuration = 1f;
    
    // Heart Events
    public event Action<int, int> OnHeartsChanged; // (currentHearts, maxHearts)
    public event Action OnDamaged;
    public event Action OnHealed;
    
    // Heart Properties
    public int CurrentHearts { get; private set; }
    public int MaxHearts => maxHearts;
    public bool IsInvincible => invincibilityTimeLeft > 0f;
    
    private float invincibilityTimeLeft = 0f;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private bool isGrounded;
    private bool isTouchingWallLeft;
    private bool isTouchingWallRight;
    private bool isDead;

    private float horizontalInput;
    private bool jumpHeld;
    private float coyoteTimeLeft;
    private float jumpBufferTimeLeft;
    private bool isRising;           // true nach Sprung bis vy <= 0
    private bool jumpCutThisFrame;   // Space losgelassen während vy > 0
    
    // Wall Jump State
    private float wallCoyoteTimeLeft;
    private int lastWallDirection;   // -1 = links, 1 = rechts, 0 = keine
    private float wallJumpInputLockTimeLeft;
    private bool isWallSliding;

    public bool IsGrounded => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsTouchingWall => isTouchingWallLeft || isTouchingWallRight;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        
        // Hearts initialisieren
        CurrentHearts = maxHearts;
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetKey(KeyCode.Space);

        jumpBufferTimeLeft -= Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferTimeLeft = jumpBufferTime;

        if (Input.GetKeyUp(KeyCode.Space))
            jumpCutThisFrame = true;
        
        // Invincibility Timer
        if (invincibilityTimeLeft > 0f)
            invincibilityTimeLeft -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (isDead || (GameManager.Instance != null && !GameManager.Instance.IsPlaying()))
            return;

        CheckGround();
        CheckWalls();

        // Coyote Time
        coyoteTimeLeft -= Time.fixedDeltaTime;
        if (isGrounded) coyoteTimeLeft = coyoteTime;

        // Wall Coyote Time
        wallCoyoteTimeLeft -= Time.fixedDeltaTime;
        UpdateWallState();
        
        // Input Lock Timer
        if (wallJumpInputLockTimeLeft > 0f)
            wallJumpInputLockTimeLeft -= Time.fixedDeltaTime;

        if (rb.linearVelocity.y <= 0f) isRising = false;

        HandleJumpInput();
        HandleWallJump();
        HandleMovement();
        HandleWallSlide();
        HandleGravity();

        jumpCutThisFrame = false;
    }

    void CheckGround()
    {
        // BoxCast nach UNTEN - ignoriert Wände an der Seite
        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)playerCollider.bounds.center;
        Vector2 boxSize = new Vector2(groundCheckWidth, 0.02f);
        
        // Kombiniere Ground und Enemy Layer
        LayerMask combinedLayers = groundLayer | enemyLayer;
        
        // Kleine Box, castet nach unten
        RaycastHit2D hit = Physics2D.BoxCast(
            origin,
            boxSize,
            0f,
            Vector2.down,
            groundCheckDistance,
            combinedLayers
        );
        
        isGrounded = hit.collider != null;
    }

    void CheckWalls()
    {
        Bounds b = playerCollider.bounds;
        Vector2 center = b.center;
        isTouchingWallLeft = Physics2D.Raycast(center, Vector2.left, b.extents.x + wallCheckDistance, groundLayer);
        isTouchingWallRight = Physics2D.Raycast(center, Vector2.right, b.extents.x + wallCheckDistance, groundLayer);
    }
    
    void UpdateWallState()
    {
        // Wall Coyote: Merken welche Wand wir zuletzt berührt haben
        if (isTouchingWallLeft && !isGrounded)
        {
            lastWallDirection = -1;
            wallCoyoteTimeLeft = wallCoyoteTime;
        }
        else if (isTouchingWallRight && !isGrounded)
        {
            lastWallDirection = 1;
            wallCoyoteTimeLeft = wallCoyoteTime;
        }
        else if (wallCoyoteTimeLeft <= 0f)
        {
            lastWallDirection = 0;
        }
        
        // Wall Sliding Check
        isWallSliding = enableWallSlide && 
                        !isGrounded && 
                        (isTouchingWallLeft || isTouchingWallRight) && 
                        rb.linearVelocity.y < 0f;
    }
    
    void HandleWallJump()
    {
        if (!enableWallJump) return;
        if (jumpBufferTimeLeft <= 0f) return;
        if (isGrounded) return; // Normaler Jump hat Priorität
        
        // Kann Wall Jump ausführen?
        bool canWallJump = (isTouchingWallLeft || isTouchingWallRight || wallCoyoteTimeLeft > 0f);
        if (!canWallJump) return;
        
        // Welche Richtung?
        int wallDir = 0;
        if (isTouchingWallLeft) wallDir = -1;
        else if (isTouchingWallRight) wallDir = 1;
        else wallDir = lastWallDirection;
        
        if (wallDir == 0) return;
        
        // Wall Jump ausführen!
        jumpBufferTimeLeft = 0f;
        wallCoyoteTimeLeft = 0f;
        
        // Kraft weg von der Wand + nach oben
        Vector2 jumpVelocity = new Vector2(-wallDir * wallJumpForceX, wallJumpForceY);
        rb.linearVelocity = jumpVelocity;
        
        // Input Lock aktivieren (verhindert sofortiges Zurück zur Wand)
        wallJumpInputLockTimeLeft = wallJumpInputLockTime;
        
        isRising = true;
    }
    
    void HandleWallSlide()
    {
        if (!isWallSliding) return;
        
        // Fallgeschwindigkeit begrenzen
        if (rb.linearVelocity.y < -wallSlideMaxSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideMaxSpeed);
        }
    }

    void HandleMovement()
    {
        // Input modifizieren wenn Wall Jump Input Lock aktiv
        float effectiveInput = horizontalInput;
        if (wallJumpInputLockTimeLeft > 0f)
        {
            // Input in Richtung der letzten Wand reduzieren
            // Erlaubt aber Input weg von der Wand
            float lockStrength = wallJumpInputLockTimeLeft / wallJumpInputLockTime;
            if ((lastWallDirection < 0 && horizontalInput < 0) ||
                (lastWallDirection > 0 && horizontalInput > 0))
            {
                effectiveInput *= (1f - lockStrength * 0.8f); // 80% reduziert
            }
        }
        
        float targetSpeed = effectiveInput * moveSpeed;
        float vx = rb.linearVelocity.x;

        if (isGrounded)
        {
            if (effectiveInput > 0 && isTouchingWallRight) targetSpeed = 0f;
            if (effectiveInput < 0 && isTouchingWallLeft) targetSpeed = 0f;
        }
        else
        {
            if (effectiveInput > 0 && isTouchingWallRight) targetSpeed = vx;
            else if (effectiveInput < 0 && isTouchingWallLeft) targetSpeed = vx;
        }

        float accelRate = isGrounded
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
            : (Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : airDeceleration);

        if (!isGrounded && Mathf.Abs(rb.linearVelocity.y) < jumpHangThreshold)
            accelRate *= jumpHangAccelMult;

        float speedDif = targetSpeed - vx;
        float movement = speedDif * accelRate;

        // Optional: Überkorrektur verhindern (wie bei Dawnosaur Slide)
        float maxMove = Mathf.Abs(speedDif) * rb.mass / Time.fixedDeltaTime;
        movement = Mathf.Clamp(movement, -maxMove, maxMove);

        rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
    }

    void HandleJumpInput()
    {
        bool canJump = (isGrounded || coyoteTimeLeft > 0f) && jumpBufferTimeLeft > 0f;
        if (!canJump) return;

        jumpBufferTimeLeft = 0f;
        coyoteTimeLeft = 0f;

        // Velocity direkt setzen (kein AddForce) → immer v.y = jumpForce, sonst wird bei Fall zu hoch
        Vector2 v = rb.linearVelocity;
        v.y = jumpForce;
        rb.linearVelocity = v;
        isRising = true;
    }

    void HandleGravity()
    {
        float vy = rb.linearVelocity.y;

        if (jumpCutThisFrame && vy > 0f)
            rb.gravityScale = gravityFall * jumpCutGravityMult;
        else if (vy > 0f && jumpHeld)
            rb.gravityScale = gravityRiseHold;
        else if (vy < 0f && Input.GetAxisRaw("Vertical") < 0f)
            rb.gravityScale = gravityFall * fastFallGravityMult;
        else if (Mathf.Abs(vy) < jumpHangThreshold)
            rb.gravityScale = gravityFall * jumpHangGravityMult;
        else if (vy < 0f)
            rb.gravityScale = gravityFall;
        else
            rb.gravityScale = gravityFall;

        if (vy < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (GameManager.Instance != null)
            GameManager.Instance.PlayerDied();
    }
    
    /// <summary>
    /// Fügt dem Spieler Schaden zu (1 Herz pro Treffer). Bei 0 Herzen stirbt der Spieler.
    /// </summary>
    public void TakeDamage(int damage = 1)
    {
        if (isDead) return;
        if (IsInvincible) return;
        
        // Jeder Treffer = 1 Herz weg (unabhängig vom damage-Wert)
        CurrentHearts--;
        CurrentHearts = Mathf.Max(CurrentHearts, 0);
        
        // Events auslösen
        OnDamaged?.Invoke();
        OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
        
        Debug.Log($"[Player] Lost 1 heart. Hearts: {CurrentHearts}/{maxHearts}");
        
        // Tod bei 0 Herzen
        if (CurrentHearts <= 0)
        {
            Die();
            return;
        }
        
        // Invincibility aktivieren
        invincibilityTimeLeft = invincibilityDuration;
    }
    
    /// <summary>
    /// Heilt den Spieler um X Herzen.
    /// </summary>
    public void HealHearts(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;
        
        int oldHearts = CurrentHearts;
        CurrentHearts += amount;
        CurrentHearts = Mathf.Min(CurrentHearts, maxHearts);
        
        if (CurrentHearts > oldHearts)
        {
            OnHealed?.Invoke();
            OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
            Debug.Log($"[Player] Healed {CurrentHearts - oldHearts} heart(s). Hearts: {CurrentHearts}/{maxHearts}");
        }
    }
    
    /// <summary>
    /// Erhöht die maximale Herzanzahl.
    /// </summary>
    public void AddMaxHeart(int amount = 1)
    {
        maxHearts += amount;
        OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
        Debug.Log($"[Player] +{amount} max heart(s). Max: {maxHearts}");
    }
    
    /// <summary>
    /// Setzt Herzen auf Maximum zurück.
    /// </summary>
    public void ResetHearts()
    {
        CurrentHearts = maxHearts;
        invincibilityTimeLeft = 0f;
        OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
    }

    void OnDrawGizmosSelected()
    {
        // Ground Check Visualisierung (Box)
        Collider2D col = GetComponent<Collider2D>();
        Vector3 origin = groundCheck != null ? groundCheck.position : 
            (col != null ? col.bounds.center : transform.position);
        
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 boxSize = new Vector3(groundCheckWidth, 0.02f, 0f);
        Vector3 boxBottom = origin + Vector3.down * groundCheckDistance;
        Gizmos.DrawWireCube(origin, boxSize);
        Gizmos.DrawWireCube(boxBottom, boxSize);
        Gizmos.DrawLine(origin - new Vector3(groundCheckWidth / 2, 0, 0), 
                        boxBottom - new Vector3(groundCheckWidth / 2, 0, 0));
        Gizmos.DrawLine(origin + new Vector3(groundCheckWidth / 2, 0, 0), 
                        boxBottom + new Vector3(groundCheckWidth / 2, 0, 0));
        
        // Wall Check Visualisierung
        if (col != null)
        {
            Bounds b = col.bounds;
            Gizmos.color = isTouchingWallLeft ? Color.cyan : Color.gray;
            Gizmos.DrawLine(b.center, (Vector2)b.center + Vector2.left * (b.extents.x + wallCheckDistance));
            Gizmos.color = isTouchingWallRight ? Color.cyan : Color.gray;
            Gizmos.DrawLine(b.center, (Vector2)b.center + Vector2.right * (b.extents.x + wallCheckDistance));
        }
    }
}
