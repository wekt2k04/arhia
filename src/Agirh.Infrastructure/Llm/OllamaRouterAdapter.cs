using Agirh.Core.Ports;

namespace Agirh.Infrastructure.Llm;

/// <summary>
/// Routeur (STACK_TECHNIQUE.md #5) : classification d'intention via phi4-mini:3.8b.
/// La sortie du modèle n'est JAMAIS utilisée telle quelle — validée contre un enum fermé,
/// tout ce qui ne matche pas exactement retombe sur HorsPerimetre (fail-safe, pas fail-open).
/// Prompt affiné empiriquement : un prompt minimal classait à tort des questions générales
/// ("qui signe la fiche de décharge ?") comme des questions de statut personnel.
/// </summary>
public sealed class OllamaRouterAdapter : ILlmRouterPort
{
    private const string Modele = "phi4-mini:3.8b";

    private const string SystemPrompt = """
        Tu es un classifieur d'intention pour un assistant RH interne. Classe la question dans EXACTEMENT une categorie parmi les trois suivantes. Reponds UNIQUEMENT par un de ces 3 mots exacts, en majuscules, rien d'autre : DOCUMENTAIRE, STATUT_DOSSIER, HORS_PERIMETRE.

        Regle cle : si la question ne contient PAS "mon", "ma", "je", "j'ai", ou "moi", classe-la TOUJOURS en DOCUMENTAIRE (jamais STATUT_DOSSIER), meme si elle parle de dossier, fiche ou signature en general.

        DOCUMENTAIRE : question generale sur une politique, regle, procedure ou charte de l'entreprise, applicable a tout le monde.
        "Quelle est la politique de mot de passe ?" -> DOCUMENTAIRE
        "Comment se passe l'onboarding ?" -> DOCUMENTAIRE
        "Qui signe la fiche de decharge ?" -> DOCUMENTAIRE (question generale sur QUI signe, pas sur MON dossier)
        "Quels documents fournir a l'arrivee ?" -> DOCUMENTAIRE

        STATUT_DOSSIER : question sur l'avancement du dossier PERSONNEL de l'utilisateur, contient obligatoirement mon/ma/je/j'ai/moi.
        "Ou en est mon onboarding ?" -> STATUT_DOSSIER
        "Mon dossier est-il cloture ?" -> STATUT_DOSSIER
        "Il me reste quoi a faire ?" -> STATUT_DOSSIER

        HORS_PERIMETRE : toute autre question sans lien avec les politiques de l'entreprise ou un dossier onboarding/offboarding.
        "Quel temps fait-il ?" -> HORS_PERIMETRE
        "Raconte-moi une blague" -> HORS_PERIMETRE

        Reponds uniquement par DOCUMENTAIRE, STATUT_DOSSIER, ou HORS_PERIMETRE.
        """;

    private readonly OllamaClient _client;

    public OllamaRouterAdapter(OllamaClient client)
    {
        _client = client;
    }

    public async Task<IntentionConversation> ClassifierAsync(string question, CancellationToken ct = default)
    {
        var reponse = await _client.GenererAsync(Modele, SystemPrompt, question, ct);
        return ParserIntention(reponse);
    }

    private static IntentionConversation ParserIntention(string? reponseBrute)
    {
        var normalise = (reponseBrute ?? string.Empty).Trim().ToUpperInvariant();

        if (normalise.Contains("STATUT_DOSSIER"))
            return IntentionConversation.StatutDossier;

        if (normalise.Contains("DOCUMENTAIRE"))
            return IntentionConversation.QuestionDocumentaire;

        return IntentionConversation.HorsPerimetre;
    }
}
