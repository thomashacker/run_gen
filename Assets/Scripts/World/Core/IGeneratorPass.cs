using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Interface für alle Generator-Passes.
    /// Jeder Pass nimmt ChunkData an, modifiziert sie, und gibt sie zurück.
    /// </summary>
    public interface IGeneratorPass
    {
        /// <summary>
        /// Name des Passes (für Debug/Logging).
        /// </summary>
        string PassName { get; }
        
        /// <summary>
        /// Ist dieser Pass aktiviert?
        /// </summary>
        bool Enabled { get; }
        
        /// <summary>
        /// Führt den Pass auf dem Chunk aus.
        /// </summary>
        /// <param name="chunk">Die Chunk-Daten (wird modifiziert)</param>
        /// <param name="context">Kontext mit globalen Settings und Nachbar-Zugriff</param>
        /// <returns>Die modifizierten Chunk-Daten</returns>
        ChunkData Execute(ChunkData chunk, GenerationContext context);
    }
    
    /// <summary>
    /// Basis-Klasse für Generator-Passes als MonoBehaviour.
    /// Ermöglicht Konfiguration im Unity Inspector.
    /// </summary>
    public abstract class GeneratorPassBase : MonoBehaviour, IGeneratorPass
    {
        [Header("Pass Settings")]
        [SerializeField] protected string passName = "Unnamed Pass";
        [SerializeField] protected bool enabled = true;
        
        public string PassName => passName;
        public bool Enabled => enabled && gameObject.activeInHierarchy;
        
        public abstract ChunkData Execute(ChunkData chunk, GenerationContext context);
        
        /// <summary>
        /// Wird aufgerufen wenn der ChunkManager initialisiert wird.
        /// Überschreiben für einmalige Initialisierung.
        /// </summary>
        public virtual void Initialize(GenerationContext context) { }
        
        /// <summary>
        /// Debug-Visualisierung im Scene View.
        /// </summary>
        protected virtual void OnDrawGizmosSelected() { }
    }
    
    /// <summary>
    /// Basis-Klasse für Passes als ScriptableObject.
    /// Ermöglicht wiederverwendbare Pass-Assets.
    /// </summary>
    public abstract class GeneratorPassAsset : ScriptableObject, IGeneratorPass
    {
        [Header("Pass Settings")]
        [SerializeField] protected string passName = "Unnamed Pass";
        [SerializeField] protected bool enabled = true;
        
        public string PassName => passName;
        public bool Enabled => enabled;
        
        public abstract ChunkData Execute(ChunkData chunk, GenerationContext context);
        
        /// <summary>
        /// Wird aufgerufen wenn der ChunkManager initialisiert wird.
        /// </summary>
        public virtual void Initialize(GenerationContext context) { }
    }
}
