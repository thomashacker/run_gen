using UnityEngine;
using System;

/// <summary>
/// Steuert Dragon-Animationen basierend auf DragonEnemy States.
/// </summary>
public class EnemyAnimator : MonoBehaviour
{
    [Header("References (auto-filled if empty)")]
    public DragonEnemy dragon;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    
    [Header("Animation Names")]
    public string idleAnim = "Dragon_Idle";
    public string targetingAnim = "Dragon_Targeting";
    public string chargingStartAnim = "Dragon_Charging_Start";
    public string chargingAnim = "Dragon_Charging";
    public string damageAnim = "Dragon_Damage";
    public string deathAnim = "Dragon_Death";
    
    [Header("Settings")]
    [Tooltip("Dauer der Damage-Animation")]
    public float damageDuration = 0.3f;
    [Tooltip("Dauer der Charging_Start Animation bevor zu Charging gewechselt wird")]
    public float chargingStartDuration = 0.2f;
    
    private string currentAnim = "";
    private float damageTimer = 0f;
    private float chargingStartTimer = 0f;
    private bool wasAiming = false;
    private bool isDying = false;
    
    void Awake()
    {
        // Auto-find References
        if (dragon == null)
            dragon = GetComponent<DragonEnemy>();
        
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }
    
    void Start()
    {
        if (dragon != null)
        {
            dragon.OnDamaged += OnDamaged;
            dragon.OnDeath += OnDeath;
        }
    }
    
    void OnDestroy()
    {
        if (dragon != null)
        {
            dragon.OnDamaged -= OnDamaged;
            dragon.OnDeath -= OnDeath;
        }
    }
    
    void OnDamaged()
    {
        damageTimer = damageDuration;
    }
    
    void OnDeath()
    {
        isDying = true;
        PlayAnimation(deathAnim);
    }
    
    void Update()
    {
        if (dragon == null || animator == null) return;
        
        // Timer runterzählen
        if (damageTimer > 0f)
            damageTimer -= Time.deltaTime;
        
        if (chargingStartTimer > 0f)
            chargingStartTimer -= Time.deltaTime;
        
        UpdateAnimation();
    }
    
    void UpdateAnimation()
    {
        string targetAnim = DetermineAnimation();
        
        if (targetAnim != currentAnim)
        {
            currentAnim = targetAnim;
            animator.Play(targetAnim);
        }
    }
    
    string DetermineAnimation()
    {
        // Priorität 1: Death
        if (isDying)
        {
            return deathAnim;
        }
        
        // Priorität 2: Damage
        if (damageTimer > 0f)
        {
            return damageAnim;
        }
        
        var state = dragon.CurrentState;
        bool playerInRange = dragon.IsPlayerInRange;
        bool isOnCooldown = dragon.IsOnCooldown;
        
        // Priorität 3: Charging (Aim State)
        if (state == DragonEnemy.DragonState.Aim)
        {
            // Charging_Start wenn wir gerade angefangen haben zu zielen
            if (!wasAiming)
            {
                wasAiming = true;
                chargingStartTimer = chargingStartDuration;
            }
            
            if (chargingStartTimer > 0f)
            {
                return chargingStartAnim;
            }
            
            return chargingAnim;
        }
        else
        {
            wasAiming = false;
        }
        
        // Priorität 4: Targeting (Spieler in Range, aber Cooldown oder keine Sicht)
        if (playerInRange && (isOnCooldown || state == DragonEnemy.DragonState.Idle))
        {
            return targetingAnim;
        }
        
        // Priorität 5: Idle
        return idleAnim;
    }
    
    /// <summary>
    /// Spielt eine Animation direkt ab.
    /// </summary>
    public void PlayAnimation(string animName)
    {
        if (animator == null) return;
        currentAnim = animName;
        animator.Play(animName);
    }
}
