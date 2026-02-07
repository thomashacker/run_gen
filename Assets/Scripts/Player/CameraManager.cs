using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                 // Der Spieler (im Inspector zuweisen)
    
    [Header("Follow Settings")]
    public float smoothSpeed = 5f;           // Wie smooth die Kamera folgt (höher = schneller)
    public Vector2 offset = new Vector2(3f, 1f);  // Offset vom Spieler (X = voraus, Y = über)
    
    [Header("Vertical Limits")]
    public float minVisibleY = 0f;           // Untere sichtbare Grenze (Kamera zeigt nichts darunter)
    public float maxY = 50f;                 // Maximale Y-Position der Kamera
    
    [Header("Look Ahead (Optional)")]
    public bool enableLookAhead = true;      // Kamera schaut etwas voraus wenn Spieler sich bewegt
    public float lookAheadAmount = 2f;       // Wie weit vorausschauen
    public float lookAheadSpeed = 3f;        // Wie schnell die Kamera vorausschaut
    
    private float currentLookAhead = 0f;
    private Vector3 velocity = Vector3.zero;
    private Camera cam;
    
    void Awake()
    {
        cam = GetComponent<Camera>();
    }
    
    void Start()
    {
        // Spieler automatisch finden falls nicht zugewiesen
        if (target == null)
        {
            PlayerManager pm = FindAnyObjectByType<PlayerManager>();
            if (pm != null)
            {
                target = pm.transform;
            }
        }
        
        // Kamera sofort auf Spieler setzen
        if (target != null)
        {
            SnapToTarget();
        }
    }
    
    void LateUpdate()
    {
        if (target == null) return;
        
        // === X-POSITION ===
        float targetX = target.position.x + offset.x;
        
        // Optional: Look Ahead basierend auf Spieler-Bewegung
        if (enableLookAhead)
        {
            Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
            if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
            {
                float targetLookAhead = Mathf.Sign(rb.linearVelocity.x) * lookAheadAmount;
                currentLookAhead = Mathf.Lerp(currentLookAhead, targetLookAhead, lookAheadSpeed * Time.deltaTime);
                targetX += currentLookAhead;
            }
        }
        
        // === Y-POSITION ===
        float targetY = target.position.y + offset.y;
        
        // Minimum Y basierend auf Kamera-Höhe berechnen
        // Die untere Kante der Kamera soll nicht unter minVisibleY gehen
        float cameraHalfHeight = cam != null ? cam.orthographicSize : 5f;
        float minCameraY = minVisibleY + cameraHalfHeight;
        
        // Y-Position clampen
        targetY = Mathf.Clamp(targetY, minCameraY, maxY);
        
        // === SMOOTH FOLLOW ===
        Vector3 targetPosition = new Vector3(
            targetX,
            targetY,
            transform.position.z  // Z bleibt (Kamera-Distanz)
        );
        
        // SmoothDamp für extra smoothes Gefühl
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            1f / smoothSpeed
        );
    }
    
    /// <summary>
    /// Setzt die Kamera sofort auf die Ziel-Position (ohne Lerp)
    /// Nützlich beim Spielstart oder Respawn
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        
        float cameraHalfHeight = cam != null ? cam.orthographicSize : 5f;
        float minCameraY = minVisibleY + cameraHalfHeight;
        
        float targetX = target.position.x + offset.x;
        float targetY = Mathf.Clamp(target.position.y + offset.y, minCameraY, maxY);
        
        transform.position = new Vector3(targetX, targetY, transform.position.z);
        velocity = Vector3.zero;
        currentLookAhead = 0f;
    }
}
