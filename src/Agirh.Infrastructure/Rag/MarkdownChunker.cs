using System.Text.RegularExpressions;

namespace Agirh.Infrastructure.Rag;

public sealed record ChunkBrut(string CheminTitres, string Contenu, int NombreTokens);

/// <summary>
/// Decoupage structurel (docs/STACK_TECHNIQUE.md phase 1) : un chunk = une section Markdown
/// (chemin de titres H1&gt;H2&gt;H3 prefixe au contenu pour le contexte), sous-decoupee par
/// paragraphe avec recouvrement si elle depasse le budget de tokens.
/// </summary>
public sealed class MarkdownChunker
{
    private readonly Func<string, int> _compterTokens;
    private readonly int _maxTokensParChunk;
    private readonly double _tauxRecouvrement;

    public MarkdownChunker(Func<string, int> compterTokens, int maxTokensParChunk = 400, double tauxRecouvrement = 0.15)
    {
        if (maxTokensParChunk <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokensParChunk), "Le budget de tokens doit être positif.");
        if (tauxRecouvrement < 0 || tauxRecouvrement >= 1)
            throw new ArgumentOutOfRangeException(nameof(tauxRecouvrement), "Le taux de recouvrement doit être dans [0, 1[.");

        _compterTokens = compterTokens ?? throw new ArgumentNullException(nameof(compterTokens));
        _maxTokensParChunk = maxTokensParChunk;
        _tauxRecouvrement = tauxRecouvrement;
    }

    public IReadOnlyList<ChunkBrut> Decouper(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return Array.Empty<ChunkBrut>();

        var resultat = new List<ChunkBrut>();

        foreach (var section in ExtraireSections(markdown))
        {
            var texteAvecTitre = ComposerTexte(section.CheminTitres, section.Corps);
            var nbTokens = _compterTokens(texteAvecTitre);

            if (nbTokens <= _maxTokensParChunk)
            {
                resultat.Add(new ChunkBrut(section.CheminTitres, texteAvecTitre, nbTokens));
            }
            else
            {
                resultat.AddRange(DecouperSectionLongue(section));
            }
        }

        return resultat;
    }

    private List<ChunkBrut> DecouperSectionLongue(Section section)
    {
        var paragraphes = section.Corps
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        var sousChunks = new List<ChunkBrut>();
        var paragraphesCourants = new List<string>();

        void EmettreSousChunk()
        {
            if (paragraphesCourants.Count == 0) return;
            var texte = ComposerTexte(section.CheminTitres, string.Join("\n\n", paragraphesCourants));
            sousChunks.Add(new ChunkBrut(section.CheminTitres, texte, _compterTokens(texte)));
        }

        for (var i = 0; i < paragraphes.Count; i++)
        {
            paragraphesCourants.Add(paragraphes[i]);
            var texteCourant = ComposerTexte(section.CheminTitres, string.Join("\n\n", paragraphesCourants));
            var estDernierParagraphe = i == paragraphes.Count - 1;

            if (_compterTokens(texteCourant) < _maxTokensParChunk && !estDernierParagraphe)
                continue;

            EmettreSousChunk();

            if (estDernierParagraphe)
            {
                // Rien de plus a ajouter : ne pas conserver de recouvrement qui ne ferait
                // que reemettre a l'identique le meme contenu (cf. tests boundary).
                paragraphesCourants = new List<string>();
                break;
            }

            var nbParagraphesRecouvrement = Math.Max(1, (int)(paragraphesCourants.Count * _tauxRecouvrement));
            paragraphesCourants = paragraphesCourants
                .Skip(Math.Max(0, paragraphesCourants.Count - nbParagraphesRecouvrement))
                .ToList();
        }

        return sousChunks;
    }

    private static string ComposerTexte(string cheminTitres, string corps) =>
        $"{cheminTitres}\n\n{corps}".Trim();

    private static List<Section> ExtraireSections(string markdown)
    {
        var lignes = markdown.Replace("\r\n", "\n").Split('\n');
        var sections = new List<Section>();
        var pileTitres = new List<(int Niveau, string Texte)>();
        var corpsCourant = new List<string>();
        var cheminTitresCourant = string.Empty;

        void ClorreSection()
        {
            var corps = string.Join("\n", corpsCourant).Trim();
            if (corps.Length > 0)
                sections.Add(new Section(cheminTitresCourant, corps));
            corpsCourant.Clear();
        }

        foreach (var ligne in lignes)
        {
            var match = Regex.Match(ligne, @"^(#{1,6})\s+(.*)$");
            if (match.Success)
            {
                ClorreSection();

                var niveau = match.Groups[1].Value.Length;
                var texte = match.Groups[2].Value.Trim();

                while (pileTitres.Count > 0 && pileTitres[^1].Niveau >= niveau)
                    pileTitres.RemoveAt(pileTitres.Count - 1);

                pileTitres.Add((niveau, texte));
                cheminTitresCourant = string.Join(" > ", pileTitres.Select(t => t.Texte));
            }
            else
            {
                corpsCourant.Add(ligne);
            }
        }

        ClorreSection();

        return sections;
    }

    private sealed record Section(string CheminTitres, string Corps);
}
