using UnityEngine;

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

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.2f;
    public Transform groundCheck;

    [Header("Wall Check (optional)")]
    public float wallCheckDistance = 0.1f;

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

    public bool IsGrounded => isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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
    }

    void FixedUpdate()
    {
        if (isDead || (GameManager.Instance != null && !GameManager.Instance.IsPlaying()))
            return;

        CheckGround();
        CheckWalls();

        coyoteTimeLeft -= Time.fixedDeltaTime;
        if (isGrounded) coyoteTimeLeft = coyoteTime;

        if (rb.linearVelocity.y <= 0f) isRising = false;

        HandleJumpInput();
        HandleMovement();
        HandleGravity();

        jumpCutThisFrame = false;
    }

    void CheckGround()
    {
        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : rb.position;
        isGrounded = Physics2D.OverlapCircle(origin, groundCheckRadius, groundLayer) != null;
    }

    void CheckWalls()
    {
        Bounds b = playerCollider.bounds;
        Vector2 center = b.center;
        isTouchingWallLeft = Physics2D.Raycast(center, Vector2.left, b.extents.x + wallCheckDistance, groundLayer);
        isTouchingWallRight = Physics2D.Raycast(center, Vector2.right, b.extents.x + wallCheckDistance, groundLayer);
    }

    void HandleMovement()
    {
        float targetSpeed = horizontalInput * moveSpeed;
        float vx = rb.linearVelocity.x;

        if (isGrounded)
        {
            if (horizontalInput > 0 && isTouchingWallRight) targetSpeed = 0f;
            if (horizontalInput < 0 && isTouchingWallLeft) targetSpeed = 0f;
        }
        else
        {
            if (horizontalInput > 0 && isTouchingWallRight) targetSpeed = vx;
            else if (horizontalInput < 0 && isTouchingWallLeft) targetSpeed = vx;
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

#if UNITY_EDITOR
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 220, 60));
        GUILayout.Label($"Grounded: {isGrounded}");
        GUILayout.Label($"Velocity: {rb.linearVelocity}");
        GUILayout.EndArea();
    }
#endif

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
