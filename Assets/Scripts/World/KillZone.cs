using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class KillZone : MonoBehaviour
{
    public enum KillZoneType
    {
        Bottom,     // Unter dem Spieler (für Runterfallen)
        Left        // Verfolgt den Spieler von links (für Zurückbleiben)
    }
    
    [Header("Type")]
    public KillZoneType zoneType = KillZoneType.Bottom;
    
    [Header("Size")]
    public float zoneWidth = 50f;
    public float zoneHeight = 2f;
    
    [Header("Left Chase Settings")]
    public float startDelay = 5f;            // Sekunden bevor die Zone anfängt sich zu bewegen
    public float startSpeed = 2f;            // Anfangsgeschwindigkeit
    [Tooltip("Speed factor relative to player max speed. 0.1 = 10% faster, -0.1 = 10% slower. Final max speed = playerSpeed * (1 + factor)")]
    public float speedFactor = 0.1f;
    [Tooltip("How fast the kill zone accelerates toward its max speed (units/sec²). Lower = slower ramp-up.")]
    public float accelerationRate = 0.5f;
    
    [Header("References")]
    public Transform player;
    
    // Public Properties
    public float CurrentSpeed => currentSpeed;
    public float MaxSpeed => maxSpeed;
    public bool IsChasing => isChasing;
    
    private BoxCollider2D boxCollider;
    private PlayerManager playerManager;
    private float initialX;
    private float initialY;
    private float currentSpeed;
    private float maxSpeed;
    private bool isChasing = false;
    private float delayTimer;
    
    void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector2(zoneWidth, zoneHeight);
        
        // Editor-Position speichern
        initialX = transform.position.x;
        initialY = transform.position.y;
    }
    
    void Start()
    {
        // Spieler automatisch finden falls nicht zugewiesen
        if (player == null)
        {
            PlayerManager pm = FindAnyObjectByType<PlayerManager>();
            if (pm != null)
            {
                player = pm.transform;
                playerManager = pm;
            }
        }
        else
        {
            playerManager = player.GetComponent<PlayerManager>();
        }
        
        // Initialisierung
        currentSpeed = startSpeed;
        maxSpeed = CalculateMaxSpeed();
        delayTimer = startDelay;
    }
    
    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            return;
        
        if (player == null) return;
        
        Vector3 newPos = transform.position;
        
        switch (zoneType)
        {
            case KillZoneType.Bottom:
                UpdateBottomZone(ref newPos);
                break;
                
            case KillZoneType.Left:
                UpdateLeftZone(ref newPos);
                break;
        }
        
        transform.position = newPos;
    }
    
    void UpdateBottomZone(ref Vector3 pos)
    {
        // X folgt dem Spieler, Y bleibt auf Editor-Position
        pos.x = player.position.x;
        pos.y = initialY;
    }
    
    void UpdateLeftZone(ref Vector3 pos)
    {
        // Delay abwarten
        if (!isChasing)
        {
            delayTimer -= Time.deltaTime;
            if (delayTimer <= 0)
            {
                isChasing = true;
            }
            else
            {
                // Während Delay: Y folgt Spieler, X bleibt
                pos.y = player.position.y;
                return;
            }
        }
        
        // === CHASE MODE ===
        
        // Recalculate max speed each frame so Inspector changes + platform boost are reflected live
        maxSpeed = CalculateMaxSpeed();
        
        // Accelerate toward max speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, accelerationRate * Time.deltaTime);
        
        // Nach rechts bewegen
        pos.x += currentSpeed * Time.deltaTime;
        
        // Y folgt dem Spieler
        pos.y = player.position.y;
    }
    
    /// <summary>
    /// Calculates the kill zone's target max speed based on the player's moveSpeed.
    /// maxSpeed = playerMoveSpeed * (1 + speedFactor)
    /// </summary>
    float CalculateMaxSpeed()
    {
        if (playerManager == null) return startSpeed;
        float playerMaxSpeed = playerManager.moveSpeed;
        return playerMaxSpeed + playerMaxSpeed * speedFactor;
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerManager playerManager = other.GetComponent<PlayerManager>();
        if (playerManager != null)
        {
            playerManager.Die();
        }
    }
    
    // Debug: Zone visualisieren
    void OnDrawGizmos()
    {
        Color zoneColor = zoneType == KillZoneType.Bottom 
            ? new Color(1f, 0f, 0f, 0.3f)   // Rot für Bottom
            : new Color(1f, 0.5f, 0f, 0.3f); // Orange für Left
        
        Gizmos.color = zoneColor;
        Vector2 size = boxCollider != null ? boxCollider.size : new Vector2(zoneWidth, zoneHeight);
        Gizmos.DrawCube(transform.position, size);
        
        Gizmos.color = zoneType == KillZoneType.Bottom ? Color.red : new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(transform.position, size);
    }
}
