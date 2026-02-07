using UnityEngine;

/// <summary>
/// Stomp-Hitbox für Dragon Enemy.
/// Wenn der Spieler von oben draufspringt, stirbt der Drache.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DragonHitbox : MonoBehaviour
{
    [Header("References")]
    public DragonEnemy dragon;
    
    [Header("Stomp Settings")]
    [Tooltip("Bounce-Kraft für Spieler nach Stomp")]
    public float stompBounceForce = 10f;
    
    void Awake()
    {
        // Collider muss Trigger sein
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
        
        // Dragon finden wenn nicht zugewiesen
        if (dragon == null)
            dragon = GetComponentInParent<DragonEnemy>();
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // Spieler suchen
        PlayerManager player = other.GetComponent<PlayerManager>();
        if (player == null)
            player = other.GetComponentInParent<PlayerManager>();
        
        if (player == null) return;
        
        // Prüfen ob Spieler von oben kommt (fällt)
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null && playerRb.linearVelocity.y < 0)
        {
            // Stomp! Drache stirbt
            if (dragon != null)
            {
                dragon.OnStomped();
            }
            
            // Spieler bounced nach oben
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounceForce);
        }
    }
}
