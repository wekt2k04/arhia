using System.Text.RegularExpressions;

namespace Agirh.Core.Security;

/// <summary>
/// Masquage PII partagé (correction F2). Utilisé par le profiler (entrée LLM),
/// l'audit JSONL (message/réponse) et les logs de debug des agents pour ne
/// jamais persister en clair mots de passe, coordonnées bancaires ou NIR.
/// </summary>
public static partial class PiiRedactor
{
    [GeneratedRegex(@"(password|mot\s*de\s*passe|bank|credit\s*card|carte\s*bancaire|iban|rib|numéro\s*sécurité\s*sociale|nir)\s*[:=]?\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "fr-FR")]
    private static partial Regex SensitiveDataRegex();

    /// <summary>Remplace toute correspondance sensible par "***" (jamais d'exception).</summary>
    public static string Redact(string? raw)
        => string.IsNullOrEmpty(raw) ? raw ?? "" : SensitiveDataRegex().Replace(raw, "***");
}
