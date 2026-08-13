using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Agirh.Infrastructure.Services;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IngestionService _ingestionService;
    private readonly ILogger<AdminController> _logger;
    private static readonly string _baseKbPath;

    static AdminController()
    {
        _baseKbPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "knowledge_base");
        if (!Directory.Exists(_baseKbPath))
            _baseKbPath = Path.Combine(Directory.GetCurrentDirectory(), "knowledge_base");
        if (!Directory.Exists(_baseKbPath))
            _baseKbPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "knowledge_base");
    }

    public AdminController(IngestionService ingestionService, ILogger<AdminController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> IngestKnowledgeBase()
    {
        if (!Directory.Exists(_baseKbPath))
            return BadRequest(new { Message = $"Dossier knowledge_base introuvable" });

        _logger.LogInformation("Ingesting KB from: {Path}", _baseKbPath);
        var result = await _ingestionService.IngestDirectoryAsync(_baseKbPath);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("ingest/file")]
    public async Task<IActionResult> IngestFile([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { Message = "Paramètre 'path' requis" });

        var fullPath = Path.GetFullPath(path);
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension != ".md" && extension != ".markdown")
            return BadRequest(new { Message = "Seuls les fichiers .md sont supportés" });

        // Correction F4 — confinement : seul un fichier DANS la base de connaissance
        // est ingérable. Sans cela, un admin compromis peut lire + embedder n'importe
        // quel fichier local (.ssh, appsettings, logs) puis l'exfiltrer via le RAG.
        var kbRoot = Path.GetFullPath(Path.TrimEndingDirectorySeparator(_baseKbPath));
        if (!fullPath.StartsWith(kbRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Message = "Le fichier doit se trouver dans le dossier knowledge_base" });

        var result = await _ingestionService.IngestFileAsync(fullPath);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("ingest")]
    public async Task<IActionResult> ClearKnowledgeBase()
    {
        await _ingestionService.ClearAllAsync();
        _logger.LogWarning("KB cleared by {User}", User.FindFirstValue(ClaimTypes.NameIdentifier));
        return Ok(new { Message = "Base vidée" });
    }
}
