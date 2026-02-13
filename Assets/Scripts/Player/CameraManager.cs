using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                 // Der Spieler (im Inspector zuweisen)
    
    [Header("Follow Settings")]
    public float smoothSpeed = 5f;           // Wie smooth die Kamera folgt (höher = schneller)
    public Vector2 offset = new Vector2(3f, 1f);  // Offset vom Spieler (X = voraus, Y = über)
    public bool followY = true;              // Ob die Kamera dem Spieler auf der Y-Achse folgt
    public float fixedYPosition = 5f;        // Feste Y-Position wenn followY deaktiviert ist
    
    [Header("Vertical Limits")]
    public float minVisibleY = 0f;           // Untere sichtbare Grenze (Kamera zeigt nichts darunter)
    public float maxY = 50f;                 // Maximale Y-Position der Kamera
    
    [Header("Pixel Snapping")]
    public bool enablePixelSnapping = true;     // Snaps camera to pixel grid to prevent sub-pixel rendering
    public float pixelsPerUnit = 16f;           // Must match your sprites' PPU!
    
    [Header("Dynamic Zoom")]
    public bool enableDynamicZoom = true;    // Zoom basierend auf Spielerhöhe
    public float baseZoom = 5f;              // Basis orthographicSize (bei Y = baseHeight)
    public float baseHeight = 5f;            // Referenz-Höhe für baseZoom
    public float zoomPerUnit = 0.15f;        // Zusätzlicher Zoom pro Höheneinheit über baseHeight
    public float minZoom = 4f;               // Minimaler Zoom (max reingezoomt)
    public float maxZoom = 12f;              // Maximaler Zoom (max rausgezoomt)
    public float zoomSpeed = 3f;             // Wie smooth der Zoom ist
    
    private Vector3 velocity = Vector3.zero;
    private Camera cam;
    private float targetZoom;
    private float currentZoom;
    
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
        
        // Zoom initialisieren
        currentZoom = baseZoom;
        targetZoom = baseZoom;
        if (cam != null)
        {
            cam.orthographicSize = baseZoom;
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
        
        // === DYNAMIC ZOOM ===
        if (enableDynamicZoom && cam != null)
        {
            // Je höher der Spieler über baseHeight, desto mehr rauszoomen
            float heightDelta = target.position.y - baseHeight;
            targetZoom = baseZoom + (heightDelta * zoomPerUnit);
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            
            // Smooth Zoom
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, zoomSpeed * Time.deltaTime);
            cam.orthographicSize = currentZoom;
        }
        
        // === Y-POSITION ===
        float targetY;
        if (followY)
        {
            targetY = target.position.y + offset.y;
            
            // Minimum Y basierend auf Kamera-Höhe berechnen
            // Die untere Kante der Kamera soll nicht unter minVisibleY gehen
            float cameraHalfHeight = cam != null ? cam.orthographicSize : 5f;
            float minCameraY = minVisibleY + cameraHalfHeight;
            
            // Y-Position clampen
            targetY = Mathf.Clamp(targetY, minCameraY, maxY);
        }
        else
        {
            targetY = fixedYPosition;
        }
        
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
        
        // Snap to pixel grid to prevent sub-pixel shimmer
        if (enablePixelSnapping)
        {
            transform.position = RoundToPixel(transform.position);
        }
    }
    
    /// <summary>
    /// Setzt die Kamera sofort auf die Ziel-Position (ohne Lerp)
    /// Nützlich beim Spielstart oder Respawn
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        
        // Zoom sofort setzen
        if (enableDynamicZoom && cam != null)
        {
            float heightDelta = target.position.y - baseHeight;
            targetZoom = baseZoom + (heightDelta * zoomPerUnit);
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            currentZoom = targetZoom;
            cam.orthographicSize = currentZoom;
        }
        
        float targetX = target.position.x + offset.x;
        float targetY;
        if (followY)
        {
            float cameraHalfHeight = cam != null ? cam.orthographicSize : 5f;
            float minCameraY = minVisibleY + cameraHalfHeight;
            targetY = Mathf.Clamp(target.position.y + offset.y, minCameraY, maxY);
        }
        else
        {
            targetY = fixedYPosition;
        }
        
        Vector3 snapPos = new Vector3(targetX, targetY, transform.position.z);
        
        if (enablePixelSnapping)
        {
            snapPos = RoundToPixel(snapPos);
        }
        
        transform.position = snapPos;
        velocity = Vector3.zero;
    }
    
    /// <summary>
    /// Rounds a position to the nearest pixel boundary based on pixelsPerUnit.
    /// Prevents sub-pixel camera positions that cause shimmer/jitter in pixel art.
    /// </summary>
    private Vector3 RoundToPixel(Vector3 position)
    {
        position.x = Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit;
        position.y = Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit;
        return position;
    }
}
