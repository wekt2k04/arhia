namespace Agirh.Core.Settings;

/// <summary>
/// Configuration typée du pipeline IA (section "AI:" des appsettings), bindée à la
/// racine de composition via AddOptions. Remplace la lecture éparpillée de
/// IConfiguration dans les constructeurs de services (les défauts divergents des
/// anciens "?? \"...\"" sont centralisés ici).
/// </summary>
public sealed class AIOptions
{
    public string Endpoint { get; set; } = "";
    public string ProfilerModel { get; set; } = "phi4-mini:3.8b";
    public string SynthesizerModel { get; set; } = "phi4-mini:3.8b";
    public string CheckerModel { get; set; } = "qwen3.5:9b";
    public string WorkerModel { get; set; } = "qwen3.5:9b";
    public string EmbeddingModel { get; set; } = "embeddinggemma";

    /// <summary>
    /// Durée pendant laquelle Ollama garde un modèle chargé en VRAM après un
    /// appel (paramètre "keep_alive" de l'API Ollama). Appliqué sur les 4
    /// clients Ollama (Profiler/Synthesizer/Checker/Embedding) et sur le
    /// warmup au démarrage — évite qu'un modèle se décharge (défaut Ollama :
    /// 5 min) entre deux messages d'une même session, ce qui recréerait un
    /// cold-start (~5-6s de chargement VRAM par modèle).
    /// </summary>
    public string OllamaKeepAlive { get; set; } = "30m";

    public int MaxExtractionRetries { get; set; } = 2;
    public int MaxReflectionLoops { get; set; } = 2;
    public int EmbeddingExpectedDimension { get; set; } = 768;
    public int RagTopN { get; set; } = 5;
    public double RagSimilarityThreshold { get; set; } = 0.60;
}
