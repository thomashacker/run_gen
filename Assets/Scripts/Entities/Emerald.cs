using UnityEngine;

/// <summary>
/// Aufsammelbarer Rubin/Münze.
/// Verschwindet bei Spieler-Berührung und gibt Punkte.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Emerald : MonoBehaviour
{
    [Header("Value")]
    [Tooltip("Wert dieses Rubins")]
    public int value = 1;
    
    [Header("Effects")]
    public GameObject collectEffectPrefab;
    public AudioClip collectSound;
    
    [Header("Animation (Optional)")]
    public bool bobUpDown = true;
    public float bobSpeed = 2f;
    public float bobAmount = 0.1f;
    
    private Vector3 startPos;
    private bool collected = false;
    
    void Awake()
    {
        // Collider muss Trigger sein
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }
    
    void Start()
    {
        startPos = transform.position;
    }
    
    void Update()
    {
        // Leichtes Auf-und-Ab
        if (bobUpDown && !collected)
        {
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        
        // Nur Spieler
        PlayerManager player = other.GetComponent<PlayerManager>();
        if (player == null)
            player = other.GetComponentInParent<PlayerManager>();
        
        if (player != null)
        {
            Collect();
        }
    }
    
    void Collect()
    {
        collected = true;
        
        // GameManager informieren
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCoins(value);
        }
        
        // Effekt spawnen
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Sound abspielen
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }
        
        // Zerstören
        Destroy(gameObject);
    }
}
