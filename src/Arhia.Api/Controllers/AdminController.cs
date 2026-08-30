using Arhia.Api.Auth;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arhia.Api.Controllers;

public sealed record CorpusOptions(string Directory);

public record ReindexCorpusResponse(int DocumentsRead, int ChunksIndexed);

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IngestCorpusUseCase _ingestCorpus;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly CorpusOptions _corpusOptions;

    public AdminController(IngestCorpusUseCase ingestCorpus, ICurrentUserAccessor currentUser, CorpusOptions corpusOptions)
    {
        _ingestCorpus = ingestCorpus;
        _currentUser = currentUser;
        _corpusOptions = corpusOptions;
    }

    [HttpPost("reindex-corpus")]
    public async Task<ActionResult<ReindexCorpusResponse>> ReindexCorpus(CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        if (!Directory.Exists(_corpusOptions.Directory))
            return NotFound($"Répertoire corpus introuvable : {_corpusOptions.Directory}");

        var documents = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(_corpusOptions.Directory, "*.md"))
        {
            documents[Path.GetFileName(file)] = await System.IO.File.ReadAllTextAsync(file, ct);
        }

        try
        {
            var chunkCount = await _ingestCorpus.ExecuteAsync(actor, documents, ct);
            return Ok(new ReindexCorpusResponse(documents.Count, chunkCount));
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
    }
}
