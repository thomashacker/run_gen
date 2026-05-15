using UnityEngine;

/// <summary>
/// Squash &amp; Stretch Effekt für den Player Sprite.
/// Sitzt auf dem Sprite-Child (gleiches GameObject wie SpriteRenderer).
/// Reagiert auf Takeoff, Air-Phase, Landing.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSquashStretch : MonoBehaviour
{
    [Header("References (auto-filled if empty)")]
    public PlayerManager player;
    public Rigidbody2D rb;

    [Header("Air Stretch (procedural)")]
    [Tooltip("Wie stark die Stretch pro m/s vertikaler Geschwindigkeit skaliert")]
    public float stretchPerVelocity = 0.015f;
    [Tooltip("Max absolute Stretch-Stärke (0.2 = bis zu 1.2 Y / 0.9 X)")]
    public float maxStretch = 0.2f;
    [Tooltip("Wie schnell zur Ziel-Skala in der Luft interpoliert wird")]
    public float airLerpSpeed = 12f;

    [Header("Takeoff Squash")]
    public Vector2 takeoffScale = new Vector2(1.2f, 0.8f);
    public float takeoffDuration = 0.12f;
    [Tooltip("Minimum vy für Takeoff Punch (filtert Ledge-Drops aus)")]
    public float minTakeoffVelocity = 1f;

    [Header("Landing Squash")]
    public Vector2 landingScale = new Vector2(1.35f, 0.65f);
    public float landingDuration = 0.18f;
    [Tooltip("Minimum |vy| für Landing Punch")]
    public float minLandingVelocity = 2f;
    [Tooltip("Zusätzliche Squash-Stärke pro m/s Impact")]
    public float landingScalePerVelocity = 0.015f;

    [Header("Grounded Settle")]
    public float groundLerpSpeed = 18f;

    [Header("Speed Stretch")]
    [Tooltip("Wie stark die horizontale Stretch bei maximaler Speed wird (0.25 = 1.25 breit am Top-Speed)")]
    public float maxSpeedStretchX = 0.22f;
    [Tooltip("Wie stark die vertikale Squash bei maximaler Speed wird (0.1 = 0.9 hoch am Top-Speed)")]
    public float maxSpeedSquashY = 0.1f;
    [Tooltip("Wie schnell die Speed-Stretch der aktuellen Geschwindigkeit folgt")]
    public float speedLerpSpeed = 8f;

    private float speedFactor = 0f;

    private Vector3 baseScale = Vector3.one;
    private Vector2 currentScale = Vector2.one;
    private bool wasGrounded = true;
    private float punchTimer = 0f;
    private Vector2 punchStartScale;
    private float punchDuration;

    void Awake()
    {
        baseScale = transform.localScale;

        if (player == null)
            player = GetComponentInParent<PlayerManager>();
        if (rb == null && player != null)
            rb = player.GetComponent<Rigidbody2D>();
    }

    void LateUpdate()
    {
        if (player == null || rb == null) return;

        bool grounded = player.IsGrounded;
        float vy = rb.linearVelocity.y;

        // Takeoff: nur wenn aktiv abgesprungen (vy filtert Falls von Kante)
        if (wasGrounded && !grounded && vy > minTakeoffVelocity)
        {
            StartPunch(takeoffScale, takeoffDuration);
        }
        // Landing: Stärke skaliert mit Impact-Velocity
        else if (!wasGrounded && grounded)
        {
            float impact = Mathf.Abs(vy);
            if (impact >= minLandingVelocity)
            {
                Vector2 s = landingScale;
                float bonus = (impact - minLandingVelocity) * landingScalePerVelocity;
                s.x += bonus;
                s.y = Mathf.Max(0.3f, s.y - bonus);
                StartPunch(s, landingDuration);
            }
        }
        wasGrounded = grounded;

        // Speed-Faktor: 0 bei Base-Speed, 1 am Top-Speed (gedämpft für Smoothness)
        float speedCap = player.moveSpeed * player.maxSpeedMultiplier;
        float speedRange = Mathf.Max(0.01f, speedCap - player.moveSpeed);
        float rawSpeed = Mathf.Clamp01((Mathf.Abs(rb.linearVelocity.x) - player.moveSpeed) / speedRange);
        speedFactor = Mathf.Lerp(speedFactor, rawSpeed, 1f - Mathf.Exp(-speedLerpSpeed * Time.deltaTime));

        // Speed-getriebene Neutral-Skala: am Top-Speed gestreckt
        Vector2 speedBase = new Vector2(
            1f + maxSpeedStretchX * speedFactor,
            1f - maxSpeedSquashY * speedFactor);

        if (punchTimer > 0f)
        {
            // Punch klingt zur aktuellen Speed-Base ab (ease-out quadratic)
            punchTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(punchTimer / punchDuration);
            t = 1f - (1f - t) * (1f - t);
            currentScale = Vector2.Lerp(punchStartScale, speedBase, t);
        }
        else if (!grounded && !player.IsWallSliding)
        {
            // Vertikalgeschwindigkeit -> Stretch, addiert ZUSÄTZLICH zu Speed-Base
            float stretch = Mathf.Clamp(vy * stretchPerVelocity, -maxStretch, maxStretch);
            float abs = Mathf.Abs(stretch);
            Vector2 target = new Vector2(speedBase.x - abs * 0.5f, speedBase.y + abs);
            float k = 1f - Mathf.Exp(-airLerpSpeed * Time.deltaTime);
            currentScale = Vector2.Lerp(currentScale, target, k);
        }
        else
        {
            float k = 1f - Mathf.Exp(-groundLerpSpeed * Time.deltaTime);
            currentScale = Vector2.Lerp(currentScale, speedBase, k);
        }

        transform.localScale = new Vector3(
            baseScale.x * currentScale.x,
            baseScale.y * currentScale.y,
            baseScale.z);
    }

    void StartPunch(Vector2 scale, float duration)
    {
        punchStartScale = scale;
        punchDuration = duration;
        punchTimer = duration;
    }

    /// <summary>
    /// Externer Trigger für eigene Punches (z.B. von Skills/Hits).
    /// </summary>
    public void TriggerPunch(Vector2 scale, float duration)
    {
        StartPunch(scale, duration);
    }
}
