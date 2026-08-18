using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public sealed record CorpusOptions(string Repertoire);

public record ReindexerCorpusResponse(int DocumentsLus, int ChunksIndexes);

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IngererCorpusUseCase _ingererCorpus;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly CorpusOptions _corpusOptions;

    public AdminController(IngererCorpusUseCase ingererCorpus, ICurrentUserAccessor currentUser, CorpusOptions corpusOptions)
    {
        _ingererCorpus = ingererCorpus;
        _currentUser = currentUser;
        _corpusOptions = corpusOptions;
    }

    [HttpPost("reindex-corpus")]
    public async Task<ActionResult<ReindexerCorpusResponse>> ReindexCorpus(CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        if (!Directory.Exists(_corpusOptions.Repertoire))
            return NotFound($"Répertoire corpus introuvable : {_corpusOptions.Repertoire}");

        var documents = new Dictionary<string, string>();
        foreach (var fichier in Directory.GetFiles(_corpusOptions.Repertoire, "*.md"))
        {
            documents[Path.GetFileName(fichier)] = await System.IO.File.ReadAllTextAsync(fichier, ct);
        }

        try
        {
            var nombreChunks = await _ingererCorpus.ExecuteAsync(actor, documents, ct);
            return Ok(new ReindexerCorpusResponse(documents.Count, nombreChunks));
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
    }
}
