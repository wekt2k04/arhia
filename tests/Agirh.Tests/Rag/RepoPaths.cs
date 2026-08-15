namespace Agirh.Tests.Rag;

internal static class RepoPaths
{
    public static string Racine { get; } = TrouverRacine();

    public static string ModelesEmbedding => Path.Combine(Racine, "models", "embedding");
    public static string ModelesReranker => Path.Combine(Racine, "models", "reranker");

    private static string TrouverRacine()
    {
        var repertoire = AppContext.BaseDirectory;
        while (repertoire is not null && !File.Exists(Path.Combine(repertoire, "Agirh.sln")))
        {
            repertoire = Directory.GetParent(repertoire)?.FullName;
        }

        return repertoire ?? throw new InvalidOperationException(
            "Agirh.sln introuvable en remontant depuis " + AppContext.BaseDirectory);
    }
}
