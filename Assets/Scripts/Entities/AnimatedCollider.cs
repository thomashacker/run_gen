using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Synchronisiert den PolygonCollider2D mit der Custom Physics Shape
/// des aktuellen Sprite-Frames. Nötig weil Unity den Collider bei
/// animierten Sprites nicht automatisch aktualisiert.
///
/// Prefab-Setup:
///   - SpriteRenderer (animiertes Sprite mit Custom Physics Shapes pro Frame)
///   - PolygonCollider2D (isTrigger je nach Bedarf)
///   - AnimatedCollider (dieses Script)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class AnimatedCollider : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D polyCollider;
    private Sprite lastSprite;
    
    // Wiederverwendbare Liste für Physics Shape Punkte (vermeidet GC Allocs)
    private List<Vector2> shapePoints = new List<Vector2>();
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        polyCollider = GetComponent<PolygonCollider2D>();
    }
    
    void LateUpdate()
    {
        Sprite currentSprite = spriteRenderer.sprite;
        
        // Nur aktualisieren wenn sich das Sprite tatsächlich geändert hat
        if (currentSprite == lastSprite) return;
        lastSprite = currentSprite;
        
        if (currentSprite == null)
        {
            polyCollider.pathCount = 0;
            return;
        }
        
        int shapeCount = currentSprite.GetPhysicsShapeCount();
        polyCollider.pathCount = shapeCount;
        
        for (int i = 0; i < shapeCount; i++)
        {
            shapePoints.Clear();
            currentSprite.GetPhysicsShape(i, shapePoints);
            polyCollider.SetPath(i, shapePoints);
        }
    }
}
