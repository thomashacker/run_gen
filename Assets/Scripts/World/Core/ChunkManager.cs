using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace WorldGeneration
{
    /// <summary>
    /// Verwaltet die Chunk-Generierung und orchestriert alle Passes.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        public int chunkWidth = 20;
        public int chunkHeight = 25;
        public int chunksAhead = 3;
        public int chunksBehind = 1;
        
        [Header("Generation")]
        [Tooltip("Seed für deterministische Generierung (0 = random)")]
        public int seed = 0;
        
        [Header("Start Area")]
        [Tooltip("Spalten links vom Ursprung die flach generiert werden")]
        public int safeStartColumns = 10;
        public int safeStartHeight = 3;
        
        [Header("Passes (Reihenfolge = Ausführung)")]
        [Tooltip("Generator-Passes in Ausführungsreihenfolge")]
        public List<GeneratorPassBase> passes = new List<GeneratorPassBase>();
        
        [Header("Rendering")]
        public ChunkRenderer chunkRenderer;
        
        [Header("References")]
        public Transform player;
        
        // Singleton
        public static ChunkManager Instance { get; private set; }
        
        // Runtime
        private GenerationContext context;
        private Dictionary<int, ChunkData> chunks = new Dictionary<int, ChunkData>();
        private int leftmostChunkIndex = 0;
        private int rightmostChunkIndex = -1;
        private Grid grid;
        
        // Public Access
        public GenerationContext Context => context;
        public int ChunkWidth => chunkWidth;
        public int ChunkHeight => chunkHeight;
        
        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
            
            grid = GetComponentInParent<Grid>();
            if (grid == null)
                grid = FindAnyObjectByType<Grid>();
            
            InitializeContext();
        }
        
        void Start()
        {
            // Auto-find player
            if (player == null)
            {
                PlayerManager pm = FindAnyObjectByType<PlayerManager>();
                if (pm != null) player = pm.transform;
            }
            
            // Auto-find renderer
            if (chunkRenderer == null)
                chunkRenderer = GetComponentInChildren<ChunkRenderer>();
            
            // Auto-find passes (wenn keine zugewiesen)
            if (passes.Count == 0)
            {
                passes.AddRange(GetComponentsInChildren<GeneratorPassBase>());
            }
            
            // Passes initialisieren
            foreach (var pass in passes)
            {
                if (pass != null)
                    pass.Initialize(context);
            }
            
            // Safe Start Area generieren
            GenerateSafeStartArea();
            
            // Initiale Chunks
            UpdateChunks();
        }
        
        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
                return;
            
            if (player == null) return;
            
            UpdateChunks();
        }
        
        void InitializeContext()
        {
            context = new GenerationContext
            {
                globalSeed = seed != 0 ? seed : Random.Range(0f, 10000f),
                chunkWidth = chunkWidth,
                chunkHeight = chunkHeight,
                cellSize = grid != null ? (Vector2)grid.cellSize : Vector2.one
            };
        }
        
        void GenerateSafeStartArea()
        {
            if (chunkRenderer == null) return;
            
            // Flacher Boden links vom Ursprung (vor dem ersten Chunk)
            for (int x = -safeStartColumns; x < 0; x++)
            {
                for (int y = 0; y < safeStartHeight; y++)
                {
                    chunkRenderer.SetTile(x, y, TileData.Solid(TileLayer.Ground));
                }
            }
        }
        
        void UpdateChunks()
        {
            if (player == null) return;
            
            float cellSizeX = context.cellSize.x;
            int playerChunk = Mathf.FloorToInt(player.position.x / (chunkWidth * cellSizeX));
            
            // Chunks vor dem Spieler generieren
            int targetRightChunk = playerChunk + chunksAhead;
            while (rightmostChunkIndex < targetRightChunk)
            {
                rightmostChunkIndex++;
                GenerateChunk(rightmostChunkIndex);
            }
            
            // Alte Chunks entfernen
            int targetLeftChunk = playerChunk - chunksBehind;
            while (leftmostChunkIndex < targetLeftChunk)
            {
                RemoveChunk(leftmostChunkIndex);
                leftmostChunkIndex++;
            }
        }
        
        void GenerateChunk(int chunkIndex)
        {
            // Neuen Chunk erstellen
            ChunkData chunk = new ChunkData(chunkIndex, chunkWidth, chunkHeight);
            
            // Kontext aktualisieren mit Nachbar-Referenzen
            context.leftNeighbor = chunks.TryGetValue(chunkIndex - 1, out var left) ? left : null;
            context.rightNeighbor = chunks.TryGetValue(chunkIndex + 1, out var right) ? right : null;
            
            // Alle Passes ausführen
            foreach (var pass in passes)
            {
                if (pass != null && pass.Enabled)
                {
                    chunk = pass.Execute(chunk, context);
                }
            }
            
            // Chunk speichern
            chunk.metadata.isComplete = true;
            chunks[chunkIndex] = chunk;
            
            // Chunk rendern
            if (chunkRenderer != null)
            {
                chunkRenderer.RenderChunk(chunk);
            }
        }
        
        void RemoveChunk(int chunkIndex)
        {
            if (chunks.ContainsKey(chunkIndex))
            {
                if (chunkRenderer != null)
                {
                    chunkRenderer.ClearChunk(chunks[chunkIndex]);
                }
                chunks.Remove(chunkIndex);
            }
        }
        
        /// <summary>
        /// Holt einen Chunk nach Index (oder null wenn nicht existiert).
        /// </summary>
        public ChunkData GetChunk(int chunkIndex)
        {
            return chunks.TryGetValue(chunkIndex, out var chunk) ? chunk : null;
        }
        
        /// <summary>
        /// Holt die Oberflächen-Höhe an einer World-X Position.
        /// </summary>
        public int GetSurfaceHeightAt(float worldX)
        {
            int chunkIndex = context.WorldToChunkIndex(worldX);
            if (chunks.TryGetValue(chunkIndex, out var chunk))
            {
                int localX = context.WorldToLocalX(worldX, chunkIndex);
                if (chunk.metadata.surfaceHeights != null && 
                    localX >= 0 && localX < chunk.metadata.surfaceHeights.Length)
                {
                    return chunk.metadata.surfaceHeights[localX];
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Fügt einen Pass zur Runtime hinzu.
        /// </summary>
        public void AddPass(GeneratorPassBase pass)
        {
            if (pass != null && !passes.Contains(pass))
            {
                passes.Add(pass);
                pass.Initialize(context);
            }
        }
        
        /// <summary>
        /// Entfernt einen Pass.
        /// </summary>
        public void RemovePass(GeneratorPassBase pass)
        {
            passes.Remove(pass);
        }
        
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            // Chunk-Grenzen visualisieren
            Gizmos.color = Color.yellow;
            float cellSizeX = context?.cellSize.x ?? 1f;
            float cellSizeY = context?.cellSize.y ?? 1f;
            
            foreach (var kvp in chunks)
            {
                int chunkIndex = kvp.Key;
                float startX = chunkIndex * chunkWidth * cellSizeX;
                float endX = startX + chunkWidth * cellSizeX;
                float height = chunkHeight * cellSizeY;
                
                // Chunk-Rahmen
                Gizmos.DrawLine(new Vector3(startX, 0, 0), new Vector3(startX, height, 0));
                Gizmos.DrawLine(new Vector3(endX, 0, 0), new Vector3(endX, height, 0));
                Gizmos.DrawLine(new Vector3(startX, 0, 0), new Vector3(endX, 0, 0));
                Gizmos.DrawLine(new Vector3(startX, height, 0), new Vector3(endX, height, 0));
            }
        }
    }
}
