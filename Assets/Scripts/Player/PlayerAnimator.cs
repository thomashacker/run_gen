using UnityEngine;

/// <summary>
/// Steuert Spieler-Animationen basierend auf PlayerManager States.
/// Sitzt auf dem Player-Parent, findet Sprite/Animator im Child automatisch.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("References (auto-filled if empty)")]
    public PlayerManager player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    
    [Header("Animation Names")]
    public string runAnim = "Player_Running";
    public string jumpAnim = "Player_Jump";
    public string fallAnim = "Player_Fall";
    public string wallAnim = "Player_Wall";
    public string deadAnim = "Player_Dead";
    public string damageAnim = "Player_Damage";
    
    [Header("Settings")]
    [Tooltip("Minimum vertikale Geschwindigkeit um als 'steigend' zu gelten")]
    public float jumpThreshold = 0.1f;
    [Tooltip("Dauer der Damage-Animation")]
    public float damageDuration = 0.3f;
    
    private Rigidbody2D rb;
    private string currentAnim = "";
    private bool facingRight = true;
    public bool IsFacingRight => facingRight;
    private float damageTimer = 0f;
    
    void Awake()
    {
        // Auto-find References
        if (player == null)
            player = GetComponent<PlayerManager>();
        
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        if (player != null)
            rb = player.GetComponent<Rigidbody2D>();
    }
    
    void Start()
    {
        // Damage Event abonnieren
        if (player != null)
        {
            player.OnDamaged += OnDamaged;
        }
    }
    
    void OnDestroy()
    {
        if (player != null)
        {
            player.OnDamaged -= OnDamaged;
        }
    }
    
    void OnDamaged()
    {
        // Damage Animation starten
        damageTimer = damageDuration;
    }
    
    void Update()
    {
        if (player == null || animator == null) return;
        
        // Damage Timer runterzählen
        if (damageTimer > 0f)
        {
            damageTimer -= Time.deltaTime;
        }
        
        UpdateFacing();
        UpdateAnimation();
    }
    
    void UpdateFacing()
    {
        if (spriteRenderer == null) return;
        
        float inputX = Input.GetAxisRaw("Horizontal");
        
        // Nur flippen wenn es tatsächlich Input gibt
        if (inputX > 0.01f && !facingRight)
        {
            facingRight = true;
            spriteRenderer.flipX = false;
        }
        else if (inputX < -0.01f && facingRight)
        {
            facingRight = false;
            spriteRenderer.flipX = true;
        }
    }
    
    void UpdateAnimation()
    {
        string targetAnim = DetermineAnimation();
        
        // Nur wechseln wenn sich die Animation ändert
        if (targetAnim != currentAnim)
        {
            currentAnim = targetAnim;
            animator.Play(targetAnim);
        }
    }
    
    string DetermineAnimation()
    {
        // Priorität 1: Dead
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
        {
            return deadAnim;
        }
        
        // Priorität 2: Damage (kurze Animation bei Treffer)
        if (damageTimer > 0f)
        {
            return damageAnim;
        }
        
        // Priorität 3: Wall Sliding
        if (player.IsWallSliding)
        {
            return wallAnim;
        }
        
        // Priorität 3: In der Luft
        if (!player.IsGrounded)
        {
            float vy = rb != null ? rb.linearVelocity.y : 0f;
            
            // Steigend = Jump Animation
            if (vy > jumpThreshold)
            {
                return jumpAnim;
            }
            // Fallend = Fall Animation
            else
            {
                return fallAnim;
            }
        }
        
        // Priorität 4: Am Boden - always running (world scrolls, player never idles)
        return runAnim;
    }
    
    /// <summary>
    /// Spielt eine spezifische Animation sofort ab (für externe Trigger).
    /// </summary>
    public void PlayAnimation(string animName)
    {
        if (animator == null) return;
        currentAnim = animName;
        animator.Play(animName);
    }
    
    /// <summary>
    /// Setzt die Blickrichtung manuell.
    /// </summary>
    public void SetFacing(bool faceRight)
    {
        facingRight = faceRight;
        if (spriteRenderer != null)
            spriteRenderer.flipX = !faceRight;
    }
}
