using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agirh.Tests.Rag;

public sealed record GoldQuestion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("categorie")] string Categorie,
    [property: JsonPropertyName("sourceeAttendu")] bool SourceeAttendu,
    [property: JsonPropertyName("sourcesAcceptees")] string[] SourcesAcceptees,
    [property: JsonPropertyName("motsClesAttendus")] string[] MotsClesAttendus,
    [property: JsonPropertyName("notes")] string? Notes = null);

internal sealed record GoldQaFile(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("questions")] List<GoldQuestion> Questions);

internal static class GoldQa
{
    private static string CheminFichier => Path.Combine(RepoPaths.Racine, "rag", "eval", "gold_qa.json");

    public static IReadOnlyList<GoldQuestion> Charger()
    {
        var json = File.ReadAllText(CheminFichier);
        var fichier = JsonSerializer.Deserialize<GoldQaFile>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("rag/eval/gold_qa.json n'a pas pu être désérialisé.");
        return fichier.Questions;
    }

    public static IEnumerable<object[]> ParCategorie(string categorie) =>
        Charger().Where(q => q.Categorie == categorie).Select(q => new object[] { q });
}
