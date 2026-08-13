using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Services;

public class IngestionService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IKnowledgeDocumentRepository _docRepo;
    private readonly ILogger<IngestionService> _logger;
    private const int ChunkSize = 512;
    private const int Overlap = 128;

    public IngestionService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IKnowledgeDocumentRepository docRepo,
        ILogger<IngestionService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _docRepo = docRepo;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return new IngestionResult { Success = false, Message = $"Fichier introuvable: {filePath}" };

        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation("Ingestion started: {File}", fileName);

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(content))
            return new IngestionResult { Success = false, Message = $"Fichier vide: {fileName}" };

        var chunks = ChunkText(content, ChunkSize, Overlap);
        if (chunks.Count == 0)
            return new IngestionResult { Success = false, Message = $"Aucun chunk généré pour: {fileName}" };

        var embeddings = new List<Embedding<float>>(chunks.Count);
        const int batchSize = 10;

        try
        {
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch = chunks.Skip(i).Take(batchSize).Select(c => c.Text).ToList();

                if (i > 0)
                    await Task.Delay(200, ct);

                var generated = await _embeddingGenerator.GenerateAsync(batch, cancellationToken: ct);
                embeddings.AddRange(generated);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding failed for {File}", fileName);
            return new IngestionResult { Success = false, Message = $"Erreur d'embedding: {ex.Message}" };
        }

        var documents = new List<KnowledgeDocument>(chunks.Count);
        for (int i = 0; i < chunks.Count; i++)
        {
            var floatBytes = new byte[embeddings[i].Vector.Length * 4];
            for (int j = 0; j < embeddings[i].Vector.Length; j++)
                Buffer.BlockCopy(BitConverter.GetBytes(embeddings[i].Vector.Span[j]), 0, floatBytes, j * 4, 4);

            documents.Add(new KnowledgeDocument
            {
                Id = Guid.NewGuid(),
                Title = fileName,
                Content = content,
                ChunkText = chunks[i].Text,
                ChunkIndex = chunks[i].Index,
                SourceFile = fileName,
                Embedding = floatBytes,
                CreatedAt = DateTime.UtcNow
            });
        }

        try
        {
            await _docRepo.AddRangeAsync(documents);
            // Commit explicite : AddRangeAsync ne fait qu'ajouter au change tracker ;
            // sans SaveChanges, l'ingestion retournerait un succès mais écrirait 0 ligne.
            await _docRepo.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist chunks for {File}", fileName);
            return new IngestionResult { Success = false, Message = $"Erreur de persistence: {ex.Message}" };
        }

        _logger.LogInformation("Ingested {File}: {Count} chunks", fileName, documents.Count);
        return new IngestionResult
        {
            Success = true,
            Message = $"{documents.Count} chunks ingérés depuis {fileName}",
            ChunksCount = documents.Count
        };
    }

    public async Task<IngestionResult> IngestDirectoryAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return new IngestionResult { Success = false, Message = $"Dossier introuvable: {directoryPath}" };

        var mdFiles = Directory.GetFiles(directoryPath, "*.md", SearchOption.TopDirectoryOnly);
        if (mdFiles.Length == 0)
            return new IngestionResult { Success = false, Message = "Aucun fichier .md trouvé" };

        var totalChunks = 0;
        var errors = new List<string>();

        foreach (var file in mdFiles)
        {
            try
            {
                var result = await IngestFileAsync(file);
                if (result.Success)
                    totalChunks += result.ChunksCount;
                else
                    errors.Add($"{Path.GetFileName(file)}: {result.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                _logger.LogError(ex, "Failed to ingest {File}", file);
            }
        }

        var message = $"{totalChunks} chunks ingérés depuis {mdFiles.Length} fichiers";
        if (errors.Count > 0)
            message += $". Échecs: {string.Join("; ", errors)}";

        return new IngestionResult
        {
            Success = errors.Count == 0,
            Message = message,
            ChunksCount = totalChunks
        };
    }

    public async Task ClearAllAsync()
    {
        await _docRepo.DeleteAllAsync();
        _logger.LogWarning("Knowledge base cleared");
    }

    public static List<(string Text, int Index)> ChunkText(string text, int wordCount, int overlap)
    {
        var chunks = new List<(string Text, int Index)>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return chunks;

        var step = Math.Max(1, wordCount - overlap);
        var index = 0;

        for (int i = 0; i < words.Length; i += step)
        {
            var chunk = string.Join(" ", words.Skip(i).Take(wordCount));
            if (string.IsNullOrWhiteSpace(chunk)) continue;
            chunks.Add((chunk, index++));
            if (i + wordCount >= words.Length) break;
        }

        return chunks;
    }
}

public class IngestionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ChunksCount { get; set; }
}
