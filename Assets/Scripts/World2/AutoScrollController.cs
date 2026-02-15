using UnityEngine;

namespace World2
{
    /// <summary>
    /// Speed / distance manager for the world.
    /// 
    /// Two modes controlled by <see cref="autoScrollEnabled"/>:
    /// 
    /// AUTO-SCROLL (default):
    ///   Speed is defined by an AnimationCurve (X = distance, Y = speed).
    ///   ChunkSpawner reads CurrentSpeed and moves the world leftward.
    ///   Camera stays static.
    /// 
    /// PLAYER-DRIVEN:
    ///   The world does not move. The player walks right through static chunks.
    ///   TotalScrolled tracks the player's furthest X displacement.
    ///   CurrentSpeed is 0 (no world movement).
    ///   Camera follows the player via CameraManager.
    /// </summary>
    public class AutoScrollController : MonoBehaviour
    {
        public static AutoScrollController Instance { get; private set; }

        [Header("Mode")]
        [Tooltip("When enabled the world scrolls left automatically. " +
                 "When disabled the player traverses the world by walking right.")]
        public bool autoScrollEnabled = true;

        [Header("Speed Curve (auto-scroll only)")]
        [Tooltip("X = total distance scrolled (units), Y = scroll speed (units/second). Shape this curve to control exactly how the game speeds up.")]
        public AnimationCurve speedCurve = new AnimationCurve(
            new Keyframe(0f, 3f),
            new Keyframe(200f, 6f),
            new Keyframe(500f, 10f),
            new Keyframe(1000f, 15f),
            new Keyframe(2000f, 20f)
        );

        [Header("Player Reference (player-driven mode)")]
        [Tooltip("Required when autoScrollEnabled is false. Auto-found if left empty.")]
        public Transform player;

        /// <summary>Current scroll speed (units/s). 0 in player-driven mode.</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>
        /// Total distance progressed.
        /// Auto-scroll: accumulated from CurrentSpeed.
        /// Player-driven: furthest X displacement of the player.
        /// </summary>
        public float TotalScrolled { get; private set; }

        private float playerStartX;

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            TotalScrolled = 0f;
            CurrentSpeed = autoScrollEnabled ? speedCurve.Evaluate(0f) : 0f;

            // Auto-find player if not assigned
            if (player == null)
            {
                var pm = FindAnyObjectByType<PlayerManager>();
                if (pm != null) player = pm.transform;
            }

            if (player != null)
                playerStartX = player.position.x;
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
                return;

            if (autoScrollEnabled)
            {
                // Speed is entirely driven by the curve
                CurrentSpeed = speedCurve.Evaluate(TotalScrolled);
                TotalScrolled += CurrentSpeed * Time.deltaTime;
            }
            else
            {
                // Player-driven: no world movement, distance = player's X progress
                CurrentSpeed = 0f;
                if (player != null)
                    TotalScrolled = Mathf.Max(TotalScrolled, player.position.x - playerStartX);
            }
        }
    }
}
