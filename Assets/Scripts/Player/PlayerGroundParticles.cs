using UnityEngine;

/// <summary>
/// Spielt ein Partikelsystem während der Spieler am Boden ist UND sich bewegt.
/// Sitzt auf dem Player-Parent, das Partikelsystem ist als Child verlinkt.
/// </summary>
[DisallowMultipleComponent]
public class PlayerGroundParticles : MonoBehaviour
{
    [Header("References (auto-filled if empty)")]
    public PlayerManager player;
    public Rigidbody2D rb;
    [Tooltip("Partikelsystem (Child), das emittieren soll während Grounded + Moving")]
    public ParticleSystem particles;
    [Tooltip("Burst-Partikelsystem, das bei jedem Sprung einmal abgefeuert wird")]
    public ParticleSystem jumpParticles;

    [Header("Settings")]
    [Tooltip("Minimum |vx| damit als 'moving' gilt")]
    public float moveThreshold = 0.5f;

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerManager>();
        if (rb == null && player != null)
            rb = player.GetComponent<Rigidbody2D>();
        if (particles == null)
            particles = GetComponentInChildren<ParticleSystem>();
    }

    void OnEnable()
    {
        if (player != null)
            player.OnJumped += HandleJumped;
    }

    void OnDisable()
    {
        if (player != null)
            player.OnJumped -= HandleJumped;
    }

    void HandleJumped()
    {
        if (jumpParticles == null) return;
        jumpParticles.Play();
    }

    void Update()
    {
        if (player == null || rb == null || particles == null) return;

        bool moving = Mathf.Abs(rb.linearVelocity.x) > moveThreshold;
        bool shouldEmit = player.IsGrounded && moving;

        if (shouldEmit && !particles.isEmitting)
            particles.Play();
        else if (!shouldEmit && particles.isEmitting)
            particles.Stop(); // default = StopEmitting → laufende Partikel verblassen natürlich
    }
}
