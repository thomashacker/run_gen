using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Zeigt Herzen im Screen-UI an.
/// Spawnt volle/leere Herz-Prefabs in einem Container.
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Der PlayerManager (wird automatisch gesucht wenn leer)")]
    public PlayerManager player;
    
    [Header("Heart Prefabs")]
    [Tooltip("Prefab für volles Herz")]
    public GameObject fullHeartPrefab;
    [Tooltip("Prefab für leeres Herz")]
    public GameObject emptyHeartPrefab;
    
    [Header("Container")]
    [Tooltip("Container mit Layout Group für die Herzen")]
    public Transform heartsContainer;
    
    [Header("Animation (Optional)")]
    [Tooltip("Skaliert kurz bei Schaden")]
    public bool punchOnDamage = true;
    public float punchScale = 1.3f;
    public float punchDuration = 0.15f;
    
    private List<GameObject> heartObjects = new List<GameObject>();
    private int lastMaxHearts = 0;
    private int lastCurrentHearts = 0;
    
    // Punch Animation State
    private float punchTimer = 0f;
    private int punchHeartIndex = -1;
    
    void Awake()
    {
        // Player finden
        if (player == null)
            player = FindAnyObjectByType<PlayerManager>();
    }
    
    void Start()
    {
        if (player != null)
        {
            // Events abonnieren
            player.OnHeartsChanged += OnHeartsChanged;
            player.OnDamaged += OnDamaged;
            
            // Initial Update
            RebuildHearts(player.CurrentHearts, player.MaxHearts);
        }
    }
    
    void OnDestroy()
    {
        if (player != null)
        {
            player.OnHeartsChanged -= OnHeartsChanged;
            player.OnDamaged -= OnDamaged;
        }
    }
    
    void Update()
    {
        // Punch Animation
        if (punchTimer > 0f && punchHeartIndex >= 0 && punchHeartIndex < heartObjects.Count)
        {
            punchTimer -= Time.unscaledDeltaTime;
            float t = 1f - (punchTimer / punchDuration);
            float scale = Mathf.Lerp(punchScale, 1f, t);
            
            if (heartObjects[punchHeartIndex] != null)
            {
                heartObjects[punchHeartIndex].transform.localScale = Vector3.one * scale;
            }
            
            if (punchTimer <= 0f)
            {
                punchHeartIndex = -1;
            }
        }
    }
    
    void OnHeartsChanged(int currentHearts, int maxHearts)
    {
        // Komplett neu bauen wenn sich maxHearts ändert
        if (maxHearts != lastMaxHearts)
        {
            RebuildHearts(currentHearts, maxHearts);
        }
        else
        {
            // Nur States updaten
            UpdateHeartStates(currentHearts);
        }
    }
    
    void OnDamaged()
    {
        // Punch Animation auf das zuletzt verlorene Herz
        if (punchOnDamage && player != null)
        {
            // Das Herz das gerade leer wurde
            int heartIndex = player.CurrentHearts; // 0-indexed: wenn 2 Herzen übrig, war Index 2 das verlorene
            if (heartIndex >= 0 && heartIndex < heartObjects.Count)
            {
                punchHeartIndex = heartIndex;
                punchTimer = punchDuration;
                
                // Start-Scale setzen
                if (heartObjects[heartIndex] != null)
                {
                    heartObjects[heartIndex].transform.localScale = Vector3.one * punchScale;
                }
            }
        }
    }
    
    /// <summary>
    /// Baut alle Herzen komplett neu auf.
    /// </summary>
    void RebuildHearts(int currentHearts, int maxHearts)
    {
        // Alte Herzen löschen
        ClearHearts();
        
        // Neue Herzen erstellen
        for (int i = 0; i < maxHearts; i++)
        {
            bool isFull = i < currentHearts;
            GameObject prefab = isFull ? fullHeartPrefab : emptyHeartPrefab;
            
            if (prefab != null && heartsContainer != null)
            {
                GameObject heart = Instantiate(prefab, heartsContainer);
                heartObjects.Add(heart);
            }
        }
        
        lastMaxHearts = maxHearts;
        lastCurrentHearts = currentHearts;
    }
    
    /// <summary>
    /// Updatet nur die Herz-States ohne neu zu spawnen.
    /// </summary>
    void UpdateHeartStates(int currentHearts)
    {
        if (fullHeartPrefab == null || emptyHeartPrefab == null) return;
        
        for (int i = 0; i < heartObjects.Count; i++)
        {
            if (heartObjects[i] == null) continue;
            
            bool shouldBeFull = i < currentHearts;
            bool wasFull = i < lastCurrentHearts;
            
            // Nur ändern wenn nötig
            if (shouldBeFull != wasFull)
            {
                // Altes Herz entfernen, neues spawnen
                Vector3 scale = heartObjects[i].transform.localScale;
                int siblingIndex = heartObjects[i].transform.GetSiblingIndex();
                
                Destroy(heartObjects[i]);
                
                GameObject prefab = shouldBeFull ? fullHeartPrefab : emptyHeartPrefab;
                GameObject newHeart = Instantiate(prefab, heartsContainer);
                newHeart.transform.SetSiblingIndex(siblingIndex);
                newHeart.transform.localScale = scale;
                
                heartObjects[i] = newHeart;
            }
        }
        
        lastCurrentHearts = currentHearts;
    }
    
    /// <summary>
    /// Entfernt alle Herz-Objekte.
    /// </summary>
    void ClearHearts()
    {
        foreach (var heart in heartObjects)
        {
            if (heart != null)
                Destroy(heart);
        }
        heartObjects.Clear();
    }
    
    /// <summary>
    /// Erzwingt ein komplettes Neuaufbauen der Herzen.
    /// </summary>
    public void ForceRefresh()
    {
        if (player != null)
        {
            RebuildHearts(player.CurrentHearts, player.MaxHearts);
        }
    }
}
