using UnityEngine;

/// <summary>
/// Aufsammelbares XP-Item.
/// Verschwindet bei Spieler-Berührung und gibt XP.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Emerald : MonoBehaviour
{
    [Header("XP Value")]
    [Tooltip("XP-Wert dieses Items")]
    public int xpValue = 1;
    
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
        
        // LevelManager XP geben
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.AddXp(xpValue);
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
