using Arhia.Core.Ports;

namespace Arhia.Infrastructure.Llm;

/// <summary>
/// Routeur (docs/STACK_TECHNIQUE.md #5) : classification d'intention via phi4-mini:3.8b.
/// La sortie du modèle n'est JAMAIS utilisée telle quelle — validée contre un enum fermé,
/// tout ce qui ne matche pas exactement retombe sur HorsPerimetre (fail-safe, pas fail-open).
/// Prompt affiné empiriquement : un prompt minimal classait à tort des questions générales
/// ("qui signe la fiche de décharge ?") comme des questions de statut personnel.
/// </summary>
public sealed class OllamaRouterAdapter : ILlmRouterPort
{
    // Configurable (Ollama:RouterModel, Program.cs) pour permettre de basculer entre un profil
    // local (petits modeles) et un profil entreprise (serveur Ollama distant, modeles plus
    // capables) sans recompiler - le porteur du projet a acces a un serveur Ollama d'entreprise
    // en plus de son Ollama local. Defaut inchange si non configure.
    private readonly string _model;

    // Les libelles de sortie (DOCUMENTAIRE/STATUT_DOSSIER/SALUTATION/INCERTAIN/HORS_PERIMETRE)
    // restent en francais a dessein : c'est un contrat de prompt calibre empiriquement avec le
    // modele, pas du vocabulaire de code — les traduire risquerait de degrader silencieusement un
    // routage deja imparfait (~27% d'erreur connu, voir HANDOFF) sans aucun benefice mesurable.
    // SALUTATION et INCERTAIN sont nouveaux (ajout Greeting/Unknown) et n'ont pas encore
    // l'historique de calibration empirique du jeu de questions gold que DOCUMENTAIRE/
    // STATUT_DOSSIER/HORS_PERIMETRE ont deja — a reevaluer lors de la prochaine mesure Gold E2E,
    // pas suppose fiable d'emblee.
    private const string SystemPrompt = """
        Tu es un classifieur d'intention pour un assistant RH interne. Classe la question dans EXACTEMENT une categorie parmi les cinq suivantes. Reponds UNIQUEMENT par un de ces 5 mots exacts, en majuscules, rien d'autre : DOCUMENTAIRE, STATUT_DOSSIER, SALUTATION, INCERTAIN, HORS_PERIMETRE.

        Regle salutations (prioritaire, verifie-la en premier) : si la question est UNIQUEMENT une salutation, formule de politesse ou remerciement (bonjour, salut, bonsoir, merci, au revoir, bonne journee...), sans aucune autre question ni demande, reponds SALUTATION directement, sans appliquer la regle suivante.

        Regle cle (sinon) : si la question ne contient PAS "mon", "ma", "je", "j'ai", ou "moi", classe-la TOUJOURS en DOCUMENTAIRE (jamais STATUT_DOSSIER), meme si elle parle de dossier, fiche ou signature en general.

        DOCUMENTAIRE : question generale sur une politique, regle, procedure ou charte de l'entreprise, applicable a tout le monde.
        "Quelle est la politique de mot de passe ?" -> DOCUMENTAIRE
        "Comment se passe l'onboarding ?" -> DOCUMENTAIRE
        "Qui signe la fiche de decharge ?" -> DOCUMENTAIRE (question generale sur QUI signe, pas sur MON dossier)
        "Quels documents fournir a l'arrivee ?" -> DOCUMENTAIRE

        STATUT_DOSSIER : question sur l'avancement du dossier PERSONNEL de l'utilisateur, contient obligatoirement mon/ma/je/j'ai/moi.
        "Ou en est mon onboarding ?" -> STATUT_DOSSIER
        "Mon dossier est-il cloture ?" -> STATUT_DOSSIER
        "Il me reste quoi a faire ?" -> STATUT_DOSSIER

        SALUTATION : UNIQUEMENT une salutation, formule de politesse ou remerciement, sans aucune autre question ni demande (voir regle salutations ci-dessus — cette categorie ne s'applique qu'a ce cas precis, jamais si une vraie question accompagne la salutation).
        "Bonjour" -> SALUTATION
        "Salut !" -> SALUTATION
        "Merci, au revoir" -> SALUTATION
        "Bonne journee" -> SALUTATION

        INCERTAIN : message trop vague, trop court ou trop ambigu pour etre rattache avec confiance a une categorie precise — ni une salutation claire, ni une question comprehensible sur les politiques de l'entreprise ou un dossier, ni clairement hors sujet. En cas de doute reel entre INCERTAIN et une autre categorie, prefere INCERTAIN plutot que de deviner.
        "Aide" -> INCERTAIN
        "Je sais pas trop" -> INCERTAIN
        "Dossier ?" -> INCERTAIN
        "???" -> INCERTAIN

        HORS_PERIMETRE : question CLAIRE et COMPREHENSIBLE mais dont le sujet n'a aucun lien avec les politiques de l'entreprise ou un dossier onboarding/offboarding. Ne s'applique jamais a une salutation (-> SALUTATION) ni a un message trop flou pour etre compris (-> INCERTAIN).
        "Quel temps fait-il ?" -> HORS_PERIMETRE
        "Raconte-moi une blague" -> HORS_PERIMETRE
        "Quelle est la capitale de l'Espagne ?" -> HORS_PERIMETRE

        Reponds uniquement par DOCUMENTAIRE, STATUT_DOSSIER, SALUTATION, INCERTAIN, ou HORS_PERIMETRE.
        """;

    private readonly OllamaClient _client;

    public OllamaRouterAdapter(OllamaClient client, string model = "phi4-mini:3.8b")
    {
        _client = client;
        _model = model;
    }

    public async Task<ConversationIntent> ClassifyAsync(string question, CancellationToken ct = default)
    {
        var response = await _client.GenerateAsync(_model, SystemPrompt, question, OllamaOptions.Router, ct);
        return ParseIntent(response);
    }

    private static ConversationIntent ParseIntent(string? rawResponse)
    {
        var normalized = (rawResponse ?? string.Empty).Trim().ToUpperInvariant();

        if (normalized.Contains("STATUT_DOSSIER"))
            return ConversationIntent.CaseStatus;

        if (normalized.Contains("DOCUMENTAIRE"))
            return ConversationIntent.DocumentaryQuestion;

        if (normalized.Contains("SALUTATION"))
            return ConversationIntent.Greeting;

        if (normalized.Contains("INCERTAIN"))
            return ConversationIntent.Unknown;

        return ConversationIntent.OutOfScope;
    }
}
