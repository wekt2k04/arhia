using System.Text.RegularExpressions;

namespace Agirh.Infrastructure.Rag;

public sealed record RawChunk(string TitlePath, string Content, int TokenCount);

/// <summary>
/// Decoupage structurel (docs/STACK_TECHNIQUE.md phase 1) : un chunk = une section Markdown
/// (chemin de titres H1&gt;H2&gt;H3 prefixe au contenu pour le contexte), sous-decoupee par
/// paragraphe avec recouvrement si elle depasse le budget de tokens.
/// </summary>
public sealed class MarkdownChunker
{
    private readonly Func<string, int> _countTokens;
    private readonly int _maxTokensPerChunk;
    private readonly double _overlapRatio;

    public MarkdownChunker(Func<string, int> countTokens, int maxTokensPerChunk = 400, double overlapRatio = 0.15)
    {
        if (maxTokensPerChunk <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokensPerChunk), "Le budget de tokens doit être positif.");
        if (overlapRatio < 0 || overlapRatio >= 1)
            throw new ArgumentOutOfRangeException(nameof(overlapRatio), "Le taux de recouvrement doit être dans [0, 1[.");

        _countTokens = countTokens ?? throw new ArgumentNullException(nameof(countTokens));
        _maxTokensPerChunk = maxTokensPerChunk;
        _overlapRatio = overlapRatio;
    }

    public IReadOnlyList<RawChunk> Chunk(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return Array.Empty<RawChunk>();

        var result = new List<RawChunk>();

        foreach (var section in ExtractSections(markdown))
        {
            var textWithTitle = ComposeText(section.TitlePath, section.Body);
            var tokenCount = _countTokens(textWithTitle);

            if (tokenCount <= _maxTokensPerChunk)
            {
                result.Add(new RawChunk(section.TitlePath, textWithTitle, tokenCount));
            }
            else
            {
                result.AddRange(ChunkLongSection(section));
            }
        }

        return result;
    }

    private List<RawChunk> ChunkLongSection(Section section)
    {
        var paragraphs = section.Body
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        var subChunks = new List<RawChunk>();
        var currentParagraphs = new List<string>();

        void EmitSubChunk()
        {
            if (currentParagraphs.Count == 0) return;
            var text = ComposeText(section.TitlePath, string.Join("\n\n", currentParagraphs));
            subChunks.Add(new RawChunk(section.TitlePath, text, _countTokens(text)));
        }

        for (var i = 0; i < paragraphs.Count; i++)
        {
            currentParagraphs.Add(paragraphs[i]);
            var currentText = ComposeText(section.TitlePath, string.Join("\n\n", currentParagraphs));
            var isLastParagraph = i == paragraphs.Count - 1;

            if (_countTokens(currentText) < _maxTokensPerChunk && !isLastParagraph)
                continue;

            EmitSubChunk();

            if (isLastParagraph)
            {
                // Rien de plus a ajouter : ne pas conserver de recouvrement qui ne ferait
                // que reemettre a l'identique le meme contenu (cf. tests boundary).
                currentParagraphs = new List<string>();
                break;
            }

            var overlapParagraphCount = Math.Max(1, (int)(currentParagraphs.Count * _overlapRatio));
            currentParagraphs = currentParagraphs
                .Skip(Math.Max(0, currentParagraphs.Count - overlapParagraphCount))
                .ToList();
        }

        return subChunks;
    }

    private static string ComposeText(string titlePath, string body) =>
        $"{titlePath}\n\n{body}".Trim();

    private static List<Section> ExtractSections(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sections = new List<Section>();
        var titleStack = new List<(int Level, string Text)>();
        var currentBody = new List<string>();
        var currentTitlePath = string.Empty;

        void CloseSection()
        {
            var body = string.Join("\n", currentBody).Trim();
            if (body.Length > 0)
                sections.Add(new Section(currentTitlePath, body));
            currentBody.Clear();
        }

        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
            if (match.Success)
            {
                CloseSection();

                var level = match.Groups[1].Value.Length;
                var text = match.Groups[2].Value.Trim();

                while (titleStack.Count > 0 && titleStack[^1].Level >= level)
                    titleStack.RemoveAt(titleStack.Count - 1);

                titleStack.Add((level, text));
                currentTitlePath = string.Join(" > ", titleStack.Select(t => t.Text));
            }
            else
            {
                currentBody.Add(line);
            }
        }

        CloseSection();

        return sections;
    }

    private sealed record Section(string TitlePath, string Body);
}
