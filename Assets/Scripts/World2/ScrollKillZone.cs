using UnityEngine;

namespace World2
{
    /// <summary>
    /// Kill zone for the World2 system.
    /// 
    /// Two modes:
    ///   STATIC  -- stays in place (used in auto-scroll mode where the world moves past it).
    ///   PURSUING -- moves rightward with acceleration (used in player-driven mode to
    ///              create time pressure: stop moving and the wall catches up).
    /// 
    /// Place one to the left of the camera (or player start) and one below the chunks.
    /// If the player touches it, they die.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ScrollKillZone : MonoBehaviour
    {
        public enum KillZoneMode { Static, Pursuing }

        [Header("Mode")]
        [Tooltip("Static: stays in place (auto-scroll). Pursuing: moves rightward with acceleration (player-driven).")]
        public KillZoneMode mode = KillZoneMode.Static;

        [Header("Size")]
        [Tooltip("Width of the kill box in world units.")]
        public float zoneWidth = 10f;
        [Tooltip("Height of the kill box in world units.")]
        public float zoneHeight = 50f;

        [Header("Pursuing Settings")]
        [Tooltip("Starting speed when pursuing (units/second).")]
        public float baseSpeed = 2f;
        [Tooltip("Speed increase per second (units/s²).")]
        public float pursuingAcceleration = 0.1f;

        [Header("Max Speed (relative to player)")]
        [Tooltip("Max speed = player moveSpeed * (1 + maxSpeedFactor).\n" +
                 "0 = same as player, 0.1 = 10% faster, -0.1 = 10% slower.")]
        public float maxSpeedFactor = 0f;

        private BoxCollider2D boxCollider;
        private float currentSpeed;
        private PlayerManager cachedPlayer;

        void Awake()
        {
            boxCollider = GetComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector2(zoneWidth, zoneHeight);
        }

        void Start()
        {
            currentSpeed = baseSpeed;
            cachedPlayer = FindAnyObjectByType<PlayerManager>();
        }

        void Update()
        {
            if (mode != KillZoneMode.Pursuing) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying()) return;

            // Max speed derived from the player's moveSpeed
            float playerMaxSpeed = cachedPlayer != null ? cachedPlayer.moveSpeed : 8f;
            float effectiveMaxSpeed = playerMaxSpeed * (1f + maxSpeedFactor);

            // Accelerate up to the effective max
            currentSpeed = Mathf.Min(currentSpeed + pursuingAcceleration * Time.deltaTime, effectiveMaxSpeed);

            // Move rightward
            transform.position += Vector3.right * currentSpeed * Time.deltaTime;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            if (playerManager != null)
            {
                playerManager.Die();
            }
        }

        /// <summary>Current pursuing speed. 0 in Static mode.</summary>
        public float CurrentPursuingSpeed => mode == KillZoneMode.Pursuing ? currentSpeed : 0f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector2 size = boxCollider != null
                ? boxCollider.size
                : new Vector2(zoneWidth, zoneHeight);
            Gizmos.DrawCube(transform.position, (Vector3)size);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, (Vector3)size);
        }
    }
}
