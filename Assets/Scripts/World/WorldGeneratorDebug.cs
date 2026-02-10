using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Debug-Anzeige für die World-Generierung: Tile-Infos unter dem Mauszeiger (oder Mitte des Bildschirms).
    /// An ChunkManager-GameObject oder eigenes GameObject hängen; "Show Debug" im Inspector aktivieren.
    /// </summary>
    public class WorldGeneratorDebug : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Tile-Infos anzeigen (unter Maus oder Bildschirmmitte)")]
        public bool showDebug = false;
        [Tooltip("Taste zum Umschalten (optional, 0 = keine)")]
        public KeyCode toggleKey = KeyCode.F1;
        [Tooltip("Mausposition verwenden; wenn aus: Bildschirmmitte")]
        public bool useMousePosition = true;
        
        [Header("Anzeige")]
        public int fontSize = 14;
        public int padding = 8;
        public Color textColor = Color.yellow;
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        
        Camera cam;
        GUIStyle boxStyle;
        GUIStyle labelStyle;
        bool stylesCreated;
        
        void Start()
        {
            if (cam == null) cam = Camera.main;
        }
        
        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                showDebug = !showDebug;
            if (cam == null) cam = Camera.main;
        }
        
        void OnGUI()
        {
            if (!showDebug || ChunkManager.Instance == null) return;
            
            Vector3 worldPos = GetSampleWorldPosition();
            TileDebugInfo info = ChunkManager.Instance.GetTileInfoAt(worldPos);
            
            EnsureStyles();
            
            string text = BuildDebugText(worldPos, info);
            if (string.IsNullOrEmpty(text)) return;
            
            float maxWidth = 320f;
            GUIContent content = new GUIContent(text);
            Vector2 size = labelStyle.CalcSize(content);
            size.x = Mathf.Min(size.x + padding * 2, maxWidth);
            size.y += padding * 2;
            
            float x = useMousePosition ? Mathf.Min(Input.mousePosition.x + 12, Screen.width - size.x - 4) : (Screen.width - size.x) / 2f;
            float y = useMousePosition ? Screen.height - Input.mousePosition.y + 12 : (Screen.height - size.y) / 2f;
            y = Mathf.Clamp(y, 4, Screen.height - size.y - 4);
            
            Rect rect = new Rect(x, y, size.x, size.y);
            GUI.Box(rect, "", boxStyle);
            GUI.Label(new Rect(rect.x + padding, rect.y + padding, rect.width - padding * 2, rect.height - padding * 2), text, labelStyle);
        }
        
        Vector3 GetSampleWorldPosition()
        {
            if (cam == null) return Vector3.zero;
            Vector3 screen = useMousePosition ? (Vector3)Input.mousePosition : new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            screen.z = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }
        
        void EnsureStyles()
        {
            if (stylesCreated) return;
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, backgroundColor);
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = fontSize;
            labelStyle.normal.textColor = textColor;
            labelStyle.wordWrap = true;
            stylesCreated = true;
        }
        
        static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
        
        static string BuildDebugText(Vector3 worldPos, TileDebugInfo info)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"World: {worldPos.x:F1}, {worldPos.y:F1}");
            if (info == null)
            {
                sb.AppendLine("— Kein Tile (außerhalb / kein Chunk)");
                return sb.ToString();
            }
            sb.AppendLine($"Chunk {info.chunkIndex}  Local [{info.localX}, {info.localY}]");
            sb.AppendLine($"Tile: {info.tile.type}  Layer: {info.tile.layer}");
            sb.AppendLine($"Walkable: {info.tile.IsWalkable}  Empty: {info.tile.IsEmpty}");
            sb.AppendLine($"heightLevel: {info.tile.heightLevel}");
            if (info.surfaceHeightInColumn >= 0)
                sb.AppendLine($"Surface (col): Y={info.surfaceHeightInColumn}");
            return sb.ToString();
        }
    }
}
