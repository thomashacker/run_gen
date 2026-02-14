using UnityEngine;

namespace World2
{
    /// <summary>
    /// Stationary kill zone for the World2 scrolling system.
    /// 
    /// Just a static trigger box. Position it in the scene and set the size.
    /// Place one to the left of the camera, one below the chunks -- or wherever you want.
    /// If the player touches it, they die.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ScrollKillZone : MonoBehaviour
    {
        [Header("Size")]
        [Tooltip("Width of the kill box in world units.")]
        public float zoneWidth = 10f;
        [Tooltip("Height of the kill box in world units.")]
        public float zoneHeight = 50f;

        private BoxCollider2D boxCollider;

        void Awake()
        {
            boxCollider = GetComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector2(zoneWidth, zoneHeight);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            if (playerManager != null)
            {
                playerManager.Die();
            }
        }

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
