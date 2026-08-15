using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

/// <summary>
/// Orchestration conversationnelle (STACK_TECHNIQUE.md #5, ARCHITECTURE.md §5) : Router puis,
/// selon l'intention, pipeline RAG (embedding -> recherche Qdrant -> reranking) ou lecture
/// seule d'un WorkflowInstance. Garde-fous LOGIQUE_METIER.md §9 : informatif uniquement,
/// anti-hallucination applique en code (jamais d'appel au generateur sans chunk pertinent),
/// RBAC applique avant toute lecture de dossier.
/// </summary>
public sealed class RepondreConversationUseCase
{
    private const int TopKRecherche = 5;
    private const int TopKApresReranking = 3;
    private const float SeuilPertinenceMinimum = 0.01f; // filtre le bruit evident ; affine par IndicateursRefusGenerateur (voir RepondreDocumentaireAsync)
    private const string PhraseRefusGenerateur = "Je n'ai pas trouvé cette information";

    // Le generateur ne reprend pas toujours la formule exacte imposee par le prompt malgre la
    // consigne explicite (constate empiriquement, milestone 9 : "le contexte fourni n'indique
    // pas..." / "ne specifie pas..." en pratique aussi frequent que la phrase canonique) - liste
    // volontairement plus large qu'une seule phrase pour rester fiable face a la variabilite
    // d'un modele 3.8B.
    private static readonly string[] IndicateursRefusGenerateur =
    {
        PhraseRefusGenerateur,
        "ne spécifie pas",
        "n'indique pas",
        "ne précise pas",
        "ne mentionne pas",
        "ne contient pas cette information",
        "ne traite pas de"
    };

    private readonly ILlmRouterPort _router;
    private readonly ILlmGeneratorPort _generateur;
    private readonly IEmbeddingPort _embedding;
    private readonly IVectorSearchPort _rechercheVectorielle;
    private readonly IRerankerPort _reranker;
    private readonly ICollaborateurRepository _collaborateurs;
    private readonly IWorkflowInstanceRepository _workflowInstances;

    public RepondreConversationUseCase(
        ILlmRouterPort router,
        ILlmGeneratorPort generateur,
        IEmbeddingPort embedding,
        IVectorSearchPort rechercheVectorielle,
        IRerankerPort reranker,
        ICollaborateurRepository collaborateurs,
        IWorkflowInstanceRepository workflowInstances)
    {
        _router = router;
        _generateur = generateur;
        _embedding = embedding;
        _rechercheVectorielle = rechercheVectorielle;
        _reranker = reranker;
        _collaborateurs = collaborateurs;
        _workflowInstances = workflowInstances;
    }

    public async Task<ReponseConversation> ExecuterAsync(
        CompteUtilisateur acteur,
        string question,
        Guid? collaborateurCibleId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("La question ne peut pas être vide.", nameof(question));

        var intention = await _router.ClassifierAsync(question, ct);

        return intention switch
        {
            IntentionConversation.QuestionDocumentaire => await RepondreDocumentaireAsync(question, ct),
            IntentionConversation.StatutDossier => await RepondreStatutDossierAsync(acteur, collaborateurCibleId, ct),
            _ => ReponseHorsPerimetre()
        };
    }

    private async Task<ReponseConversation> RepondreDocumentaireAsync(string question, CancellationToken ct)
    {
        var vecteurRequete = await _embedding.GenererEmbeddingAsync(question, ct);
        var candidats = await _rechercheVectorielle.RechercherAsync(vecteurRequete, TopKRecherche, ct);

        if (candidats.Count == 0)
            return ReponseNonTrouvee();

        var rerankes = await _reranker.RerankAsync(question, candidats, ct);
        var meilleurs = rerankes
            .Where(c => c.Score >= SeuilPertinenceMinimum)
            .Take(TopKApresReranking)
            .ToList();

        if (meilleurs.Count == 0)
            return ReponseNonTrouvee();

        var contexte = string.Join(
            "\n\n---\n\n",
            meilleurs.Select(c => $"[Source: {c.DocumentSource} — {c.CheminTitres}]\n{c.Contenu}"));

        var systemPrompt =
            "Tu es l'assistant RH d'AGIRH. Le contexte ci-dessous contient des extraits de la documentation " +
            "interne déjà sélectionnés comme pertinents pour cette question. Lis-le attentivement en entier " +
            "avant de répondre : la réponse s'y trouve généralement, parfois formulée différemment de la " +
            "question. Réponds en français, de façon concise et complète, UNIQUEMENT à partir de ce contexte. " +
            $"Dis explicitement « {PhraseRefusGenerateur} » UNIQUEMENT si le contexte ne traite " +
            "vraiment pas du sujet de la question — pas simplement parce que la formulation diffère.\n\n" +
            $"Contexte :\n{contexte}";

        var texte = await _generateur.GenererReponseAsync(systemPrompt, question, ct);

        // Le score de reranking mesure la proximité thématique, pas "la réponse est présente" :
        // vérifié empiriquement (milestone 9, jeu de Q/R gold) qu'un chunk du bon sujet mais
        // muet sur le fait précis demandé peut scorer aussi haut qu'une vraie réponse (ex.
        // 0.70-0.78, proche de vrais positifs). Remonter le seuil pénaliserait autant de bonnes
        // réponses qu'il n'en filtrerait. Le signal fiable est le générateur lui-même : le
        // prompt ci-dessus lui impose la formule exacte de refus quand le contexte ne traite
        // pas du sujet — on la relit ici plutôt que de sourcer une réponse qui est en fait un
        // refus.
        if (IndicateursRefusGenerateur.Any(indicateur => texte.Contains(indicateur, StringComparison.OrdinalIgnoreCase)))
            return new ReponseConversation(texte, Sourcee: false, Array.Empty<string>());

        var sources = meilleurs.Select(c => c.DocumentSource).Distinct().ToList();
        return new ReponseConversation(texte, Sourcee: true, sources);
    }

    private async Task<ReponseConversation> RepondreStatutDossierAsync(
        CompteUtilisateur acteur,
        Guid? collaborateurCibleId,
        CancellationToken ct)
    {
        var idCible = collaborateurCibleId ?? await ResoudreDossierPersonnelAsync(acteur, ct);
        if (idCible is null)
            return ReponseDossierIntrouvable();

        var collaborateur = await _collaborateurs.ObtenirParIdAsync(idCible.Value, ct);
        if (collaborateur is null)
            return ReponseDossierIntrouvable();

        if (!PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur))
            throw new AccesRefuseException("Vous n'avez pas accès au dossier de ce collaborateur.");

        var instance = await _workflowInstances.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Onboarding, ct)
            ?? await _workflowInstances.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Offboarding, ct);

        if (instance is null)
            return new ReponseConversation(
                "Aucun dossier onboarding ou offboarding actif n'a été trouvé pour ce collaborateur.",
                Sourcee: false,
                Array.Empty<string>());

        var itemsRestants = instance.Items.Count(i => i.Etat == ItemEtat.EnAttente);
        var suffixe = itemsRestants > 0
            ? $"{itemsRestants} item(s) restent en attente sur {instance.Items.Count}."
            : "Tous les items ont été traités.";

        var texte = $"Le dossier {instance.Type} de {collaborateur.Prenom} {collaborateur.Nom} " +
                     $"est au statut {instance.Statut}. {suffixe}";

        return new ReponseConversation(texte, Sourcee: false, Array.Empty<string>());
    }

    private async Task<Guid?> ResoudreDossierPersonnelAsync(CompteUtilisateur acteur, CancellationToken ct)
    {
        if (acteur.Role != RoleType.Collaborateur)
            return null; // RH/Admin doivent préciser explicitement quel collaborateur (collaborateurCibleId)

        var collaborateur = await _collaborateurs.ObtenirParCompteUtilisateurIdAsync(acteur.Id, ct);
        return collaborateur?.Id;
    }

    private static ReponseConversation ReponseDossierIntrouvable() => new(
        "Je n'ai pas trouvé de dossier collaborateur associé à cette demande. " +
        "Si vous êtes un nouveau collaborateur, contactez le RH de votre pôle pour que votre fiche soit créée.",
        Sourcee: false,
        Array.Empty<string>());

    private static ReponseConversation ReponseNonTrouvee() => new(
        "Je n'ai pas trouvé cette information dans la documentation disponible. " +
        "Vous pouvez contacter le RH de votre pôle.",
        Sourcee: false,
        Array.Empty<string>());

    private static ReponseConversation ReponseHorsPerimetre() => new(
        "Cette question sort du périmètre de l'assistant AGIRH (onboarding/offboarding et politiques internes). " +
        "Contactez le RH de votre pôle pour toute autre demande.",
        Sourcee: false,
        Array.Empty<string>());
}
