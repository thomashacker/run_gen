using UnityEngine;

namespace World2
{
    /// <summary>
    /// Pure speed manager for the world scroll.
    /// 
    /// Speed is defined by an AnimationCurve where X = distance scrolled, Y = speed.
    /// Shape the curve in the Inspector to get exactly the ramp you want.
    /// 
    /// The ChunkSpawner reads CurrentSpeed and moves the world leftward each frame.
    /// </summary>
    public class AutoScrollController : MonoBehaviour
    {
        public static AutoScrollController Instance { get; private set; }

        [Header("Speed Curve")]
        [Tooltip("X = total distance scrolled (units), Y = scroll speed (units/second). Shape this curve to control exactly how the game speeds up.")]
        public AnimationCurve speedCurve = new AnimationCurve(
            new Keyframe(0f, 3f),
            new Keyframe(200f, 6f),
            new Keyframe(500f, 10f),
            new Keyframe(1000f, 15f),
            new Keyframe(2000f, 20f)
        );

        /// <summary>Current scroll speed read from the curve. Read by ChunkSpawner.</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>Total distance the world has scrolled since the start.</summary>
        public float TotalScrolled { get; private set; }

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
            CurrentSpeed = speedCurve.Evaluate(0f);
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
                return;

            // Speed is entirely driven by the curve
            CurrentSpeed = speedCurve.Evaluate(TotalScrolled);

            // Track total distance scrolled
            TotalScrolled += CurrentSpeed * Time.deltaTime;
        }
    }
}
