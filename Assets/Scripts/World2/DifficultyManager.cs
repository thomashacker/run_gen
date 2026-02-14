using UnityEngine;

namespace World2
{
    /// <summary>
    /// Maps elapsed time (or distance traveled) to a difficulty level from 1 to 10.
    /// Uses a configurable AnimationCurve in the Inspector for full control over the ramp.
    /// 
    /// The ChunkSpawner reads CurrentDifficulty each time it spawns a new chunk
    /// and uses it to filter which ChunkDefinitions are eligible.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        [Header("Difficulty Curve")]
        [Tooltip("X axis = input value (time in seconds, or distance in units). Y axis = difficulty (1-10). Shape the curve to control how fast difficulty ramps up.")]
        public AnimationCurve difficultyCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(60f, 3f),
            new Keyframe(120f, 5f),
            new Keyframe(180f, 7f),
            new Keyframe(300f, 10f)
        );

        [Header("Input Mode")]
        [Tooltip("If true, difficulty is based on total world scroll distance (from AutoScrollController). If false, based on elapsed time.")]
        public bool useDistance = false;

        /// <summary>Current difficulty level, clamped to 1-10 integer.</summary>
        public int CurrentDifficulty { get; private set; } = 1;

        /// <summary>Raw (float) difficulty value from the curve, before rounding.</summary>
        public float CurrentDifficultyRaw { get; private set; } = 1f;

        /// <summary>Elapsed play time in seconds.</summary>
        public float ElapsedTime => elapsedTime;

        private float elapsedTime;

        void Start()
        {
            elapsedTime = 0f;
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
                return;

            elapsedTime += Time.deltaTime;

            // Choose input for the curve
            float input;
            if (useDistance && AutoScrollController.Instance != null)
                input = AutoScrollController.Instance.TotalScrolled;
            else
                input = elapsedTime;

            // Evaluate the curve
            CurrentDifficultyRaw = difficultyCurve.Evaluate(input);
            CurrentDifficulty = Mathf.Clamp(Mathf.RoundToInt(CurrentDifficultyRaw), 1, 10);
        }
    }
}
