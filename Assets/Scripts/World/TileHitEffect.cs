using UnityEngine;
using System.Collections;

/// <summary>
/// Visueller Effekt wenn ein Tile getroffen wird.
/// Weißer Flash mit Scale Punch, dann Self-Destruct.
/// Wird von TileHealthManager gespawnt - kein Prefab nötig.
/// </summary>
public class TileHitEffect : MonoBehaviour
{
    [HideInInspector] public float duration = 0.15f;
    [HideInInspector] public float maxScale = 1.3f;
    [HideInInspector] public float startAlpha = 0.8f;
    [HideInInspector] public Vector3 cellSize = Vector3.one;
    
    // Statisch gecachter Sprite (wird nur einmal erstellt)
    private static Sprite cachedWhiteSprite;
    
    void Start()
    {
        // SpriteRenderer erstellen
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = new Color(1f, 1f, 1f, startAlpha);
        sr.sortingOrder = 100; // Über den Tilemap-Tiles rendern
        
        // Größe an Tile-Cell anpassen
        transform.localScale = new Vector3(cellSize.x, cellSize.y, 1f);
        
        StartCoroutine(AnimateEffect(sr));
    }
    
    IEnumerator AnimateEffect(SpriteRenderer sr)
    {
        float elapsed = 0f;
        Vector3 baseScale = transform.localScale;
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            // Scale Punch: schnell hoch, dann zurück (Ease Out Quad)
            // 0->0.3: Scale von 1.0 auf maxScale
            // 0.3->1.0: Scale von maxScale auf 1.0
            float scaleMult;
            if (t < 0.3f)
            {
                float scaleT = t / 0.3f;
                scaleMult = Mathf.Lerp(1f, maxScale, scaleT);
            }
            else
            {
                float scaleT = (t - 0.3f) / 0.7f;
                scaleMult = Mathf.Lerp(maxScale, 1f, scaleT * scaleT); // Ease out
            }
            
            transform.localScale = baseScale * scaleMult;
            
            // Alpha: Fade out über die gesamte Dauer
            float alpha = Mathf.Lerp(startAlpha, 0f, t * t);
            sr.color = new Color(1f, 1f, 1f, alpha);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Erstellt einen einfachen weißen 4x4 Sprite (einmalig, dann gecacht).
    /// </summary>
    static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null) return cachedWhiteSprite;
        
        // 4x4 weiße Textur erstellen
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        
        // Sprite erstellen (16 pixels per unit = 1 unit für 4x4 Textur -> passt auf 1 Tile)
        cachedWhiteSprite = Sprite.Create(
            tex,
            new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f),
            4f // pixelsPerUnit: 4 pixel / 4 ppu = 1 unit Breite
        );
        cachedWhiteSprite.name = "WhiteTileSprite";
        
        return cachedWhiteSprite;
    }
}
