# AGIRH — Vue d'ensemble pour apprentissage (priorité IA/ML)

*Document dense, pensé pour être recopié à la main en 1-2h. Chaque notion est expliquée avec le
"pourquoi", pas juste le "quoi". **La partie IA/ML (RAG + orchestration, §5-6) est le cœur de ce
document et doit être maîtrisée en profondeur** — le reste (§1-4, §7-11) est là pour situer le
contexte, à connaître mais avec moins de détail. Pour la référence exhaustive et à jour :
`docs/LOGIQUE_METIER.md`, `docs/STACK_TECHNIQUE.md`, `docs/ARCHITECTURE.md`.*

## 1. Le principe central : architecture hexagonale

Le code est rangé en 4 couches, **une couche ne dépend que de celles listées en dessous d'elle** :

```
Agirh.Domain          (entités pures)  →  Agirh.Core (ports/use cases/RBAC)
   →  Agirh.Infrastructure (adaptateurs : EF Core, Qdrant, ONNX, Ollama, SSE)  →  Agirh.Api
```

Domain ne sait même pas qu'une base de données existe. Core définit *ce dont il a besoin* via des
interfaces (`IWorkflowInstanceRepository`, `ILlmRouterPort`, `IEmbeddingPort`...) sans savoir
comment c'est fait — Infrastructure fournit l'implémentation concrète. Remplacer SQL Server ou
Ollama ne touche *que* Infrastructure. `Agirh.Api/Program.cs` est la **composition root** : seul
endroit qui choisit quel adaptateur brancher derrière chaque port.

## 2. RBAC : qui a le droit de faire quoi

3 rôles, portée croissante : **Collaborateur** (ses données), **RH** (son pôle uniquement, vérifié
par `PoleScopeGuard` *avant* le rôle), **AdminQualite** (portée globale, seul rôle qui élève un
compte ou valide un template).

```csharp
// src/Agirh.Core/Security/PoleScopeGuard.cs, lignes 8-16
public static bool PeutAccederAuPole(CompteUtilisateur acteur, Guid poleCibleId) =>
    acteur.Role switch
    {
        RoleType.AdminQualite => true,
        RoleType.RH => acteur.PoleId == poleCibleId,
        _ => false  // Collaborateur n'a jamais de portée sur un pôle entier
    };
```

## 3. Le frontend ne détient jamais le JWT — pattern BFF

Le navigateur ne parle jamais directement à `Agirh.Api` — il parle à des Route Handlers Next.js
qui appellent l'Api côté serveur avec le JWT, puis posent un **cookie httpOnly** (invisible en
JavaScript, donc invulnérable à un vol de token par XSS).

## 4. Temps réel : SSE

Flux HTTP unidirectionnel serveur→client (`EventSource` navigateur), utilisé pour le chat
(fragments de texte au fil de la génération) et les notifications (tableau JSON toutes les 10s).
**Piège réel** : la compression HTTP de Next.js bufferise tout avant d'envoyer — casse le
streaming. Fix : `compress: false` dans `next.config.ts`.

---

## 5-6. LA PARTIE IA/ML — RAG + orchestration conversationnelle

RAG (Retrieval-Augmented Generation) = au lieu de laisser un LLM répondre de mémoire (donc
halluciner), on cherche les passages pertinents dans les vrais documents et on force le modèle à
ne parler que de ça. Deux sous-systèmes **distincts et complémentaires** :

```
Pipeline RAG (Agirh.Infrastructure/Rag/) : où chercher l'information
Orchestration (Agirh.Infrastructure/Llm/ + Agirh.Core/UseCases/) : quoi faire de la question
```

### Carte des fichiers IA — qui fait quoi

| Fichier | Rôle |
|---|---|
| `src/Agirh.Infrastructure/Rag/MarkdownChunker.cs` | Phase 1 — découpe un document en chunks |
| `src/Agirh.Infrastructure/Rag/XlmRobertaTokenizer.cs` | Tokenisation partagée (embedding + reranking) |
| `src/Agirh.Infrastructure/Rag/OnnxEmbeddingAdapter.cs` | Phase 2 — texte → vecteur 768d |
| `src/Agirh.Infrastructure/Rag/QdrantVectorSearchAdapter.cs` | Phase 3 — indexation + recherche ANN |
| `src/Agirh.Infrastructure/Rag/OnnxRerankerAdapter.cs` | Phase 4 — réordonnancement cross-encodeur |
| `src/Agirh.Core/UseCases/IngererCorpusUseCase.cs` | Orchestre 1→2→3 à l'ingestion du corpus |
| `src/Agirh.Infrastructure/Llm/OllamaClient.cs` | Client HTTP bas niveau partagé vers Ollama |
| `src/Agirh.Infrastructure/Llm/OllamaRouterAdapter.cs` | Router — classification d'intention |
| `src/Agirh.Infrastructure/Llm/OllamaGeneratorAdapter.cs` | Generator — écriture de la réponse |
| `src/Agirh.Core/UseCases/RepondreConversationUseCase.cs` | Orchestrateur central + garde-fous |
| `src/Agirh.Api/Controllers/ChatController.cs` | Endpoint SSE qui déclenche tout le pipeline |
| `src/Agirh.Api/Controllers/AdminController.cs` | Endpoint de réindexation du corpus (Admin/Qualité) |

### Phase 1 — Chunking (découpage structurel + recouvrement)

Découpage **par structure** (sections/titres Markdown, pas taille fixe aveugle) **avec
recouvrement** entre chunks voisins, pour ne pas perdre une info à cheval sur une frontière.

```csharp
// src/Agirh.Infrastructure/Rag/MarkdownChunker.cs, lignes 18-28
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
```

Budget par défaut : 400 tokens/chunk, 15% de recouvrement. `Decouper()` (ligne 30) extrait les
sections, et ne sous-découpe par paragraphe (avec recouvrement) que si une section dépasse le
budget — la plupart des sections courtes restent un seul chunk.

### Phase 2 — Embedding (vectorisation, ONNX Runtime .NET pur)

Modèle **multilingue** (corpus en français), exécuté en ONNX Runtime — pas d'appel Ollama pour
cette étape, contrôle total sur la latence.

```csharp
// src/Agirh.Infrastructure/Rag/OnnxEmbeddingAdapter.cs, lignes 12-23
public sealed class OnnxEmbeddingAdapter : IEmbeddingPort, IDisposable
{
    public int Dimension { get; } = 768;

    private readonly InferenceSession _session;
    private readonly XlmRobertaTokenizer _tokenizer;

    public OnnxEmbeddingAdapter(string cheminModeleOnnx, string cheminSentencePieceModel)
    {
        _session = new InferenceSession(cheminModeleOnnx);
        _tokenizer = XlmRobertaTokenizer.ChargerDepuisFichier(cheminSentencePieceModel);
    }
```

`Dimension` (768) est une propriété vérifiable, pas une constante magique dispersée — un mismatch
avec Qdrant serait détecté explicitement plutôt que de casser silencieusement la recherche.
`GenererEmbeddingAsync` (ligne 25, pas reproduit ici) fait ensuite : tokenisation → tenseurs
`input_ids`/`attention_mask` → `InferenceSession.Run` → mean-pooling masqué + normalisation L2 sur
les embeddings de tokens → un seul vecteur de phrase.

### Phase 3 — Storage (Qdrant, ANN/HNSW, cosinus)

```csharp
// src/Agirh.Infrastructure/Rag/QdrantVectorSearchAdapter.cs, lignes 47-55
public async Task<IReadOnlyList<ChunkDocumentaire>> RechercherAsync(float[] vecteurRequete, int topK, CancellationToken ct = default)
{
    var resultats = await _client.QueryAsync(
        NomCollection, query: vecteurRequete, limit: (ulong)topK,
        payloadSelector: true, cancellationToken: ct);

    return resultats.Select(r => new ChunkDocumentaire(/* Id, DocumentSource, ChunkIndex, CheminTitres, Contenu, Score */)).ToList();
}
```

Collection `"agirh-corpus"` (ligne 9), distance cosinus fixée à l'indexation (`PreparerAsync`,
ligne 27). Recherche **ANN** (Approximate Nearest Neighbor, HNSW côté Qdrant) : on perd la
garantie d'exactitude en échange d'une vitesse largement supérieure — pas strictement nécessaire
à cette échelle de corpus, mais démontre une vraie compétence d'infra IA.

### Phase 4 — Reranking (cross-encodeur, la phase la plus déterminante)

Un **bi-encodeur** (phase 2) encode requête et document *séparément* — rapide, imprécis. Un
**cross-encodeur** (ici) les lit *ensemble*, en une seule passe — lent mais nettement plus précis.
D'où l'architecture en deux étages : bi-encodeur pour réduire vite le corpus, cross-encodeur pour
réordonner finement les survivants.

```csharp
// src/Agirh.Infrastructure/Rag/OnnxRerankerAdapter.cs, lignes 32-35 + 40-65
var resultats = candidats
    .Select(c => c with { Score = CalculerScore(requete, c.Contenu) })
    .OrderByDescending(c => c.Score)
    .ToList();

private float CalculerScore(string requete, string document)
{
    var ids = _tokenizer.EncoderPaireEnIdsHuggingFace(requete, document); // <s>requête</s></s>document</s>
    // ... tenseurs -> InferenceSession.Run ...
    var logit = resultatsOnnx.First(r => r.Name == "logits").AsTensor<float>()[0, 0];
    return Sigmoid(logit);
}

private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));
```

**Piège réel, le plus important du pipeline RAG** : un score élevé (jusqu'à 0.78 observé, seuil de
pertinence fixé à 0.01) ne garantit **pas** que le chunk contient la réponse — juste qu'il est
*thématiquement* proche. Le score mesure une proximité, pas une vérité. C'est pour ça que la
décision finale « sourcé ou non » (§6) relit le texte réellement généré, jamais le score seul.

### Router — classification d'intention (fail-safe, pas fail-open)

```csharp
// src/Agirh.Infrastructure/Llm/OllamaRouterAdapter.cs, lignes 20-41 (prompt système complet)
Tu es un classifieur d'intention pour un assistant RH interne. Classe la question dans EXACTEMENT
une categorie parmi les trois suivantes. Reponds UNIQUEMENT par un de ces 3 mots exacts, en
majuscules, rien d'autre : DOCUMENTAIRE, STATUT_DOSSIER, HORS_PERIMETRE.

Regle cle : si la question ne contient PAS "mon", "ma", "je", "j'ai", ou "moi", classe-la TOUJOURS
en DOCUMENTAIRE (jamais STATUT_DOSSIER), meme si elle parle de dossier, fiche ou signature en
general.
```
```csharp
// src/Agirh.Infrastructure/Llm/OllamaRouterAdapter.cs, lignes 57-68 (parsing de la sortie brute)
private static IntentionConversation ParserIntention(string? reponseBrute)
{
    var normalise = (reponseBrute ?? string.Empty).Trim().ToUpperInvariant();

    if (normalise.Contains("STATUT_DOSSIER"))
        return IntentionConversation.StatutDossier;
    if (normalise.Contains("DOCUMENTAIRE"))
        return IntentionConversation.QuestionDocumentaire;

    return IntentionConversation.HorsPerimetre; // tout le reste : vide, timeout, mot halluciné
}
```

**Fail-safe, pas fail-open** : la sortie brute n'est jamais utilisée telle quelle — validée contre
un enum fermé à 3 valeurs, tout ce qui ne matche pas exactement retombe sur `HorsPerimetre` par
défaut. Un flou ou une panne du LLM ne peut jamais accidentellement ouvrir l'accès à quelque chose.

**Limite mesurée et assumée** : ~27% de mauvais classement sur 48 questions de test. Doubler les
exemples few-shot dans le prompt n'a eu **aucun effet mesurable** (testé empiriquement, abandonné)
— la capacité d'un modèle 3,8B à suivre des règles explicites plafonne ; plus de contexte ne
compense pas une limite de raisonnement du modèle lui-même.

### Generator + le garde-fou anti-hallucination (le code le plus important de tout le projet)

```csharp
// src/Agirh.Core/UseCases/RepondreConversationUseCase.cs, ligne 22 + lignes 179-194 (double porte de sortie)
private const float SeuilPertinenceMinimum = 0.01f;

private async Task<PreparationDocumentaire> PreparerContexteDocumentaireAsync(string question, CancellationToken ct)
{
    var vecteurRequete = await _embedding.GenererEmbeddingAsync(question, ct);
    var candidats = await _rechercheVectorielle.RechercherAsync(vecteurRequete, TopKRecherche, ct);

    if (candidats.Count == 0)
        return new PreparationDocumentaire(false, null, Array.Empty<ChunkDocumentaire>());

    var rerankes = await _reranker.RerankAsync(question, candidats, ct);
    var meilleurs = rerankes
        .Where(c => c.Score >= SeuilPertinenceMinimum)
        .Take(TopKApresReranking)
        .ToList();

    if (meilleurs.Count == 0)
        return new PreparationDocumentaire(false, null, Array.Empty<ChunkDocumentaire>());
    // ... construction du system prompt avec le contexte trouvé, puis appel au Generator ...
}
```

**La règle à retenir avant tout le reste** : il y a **deux portes de sortie anticipée** avant même
d'appeler le Generator — `candidats.Count == 0` (rien trouvé dans Qdrant) et `meilleurs.Count == 0`
(rien ne passe le seuil après reranking). Si l'une ou l'autre se déclenche, le Generator **n'est
jamais invoqué** : le système répond directement "je n'ai pas trouvé cette information". C'est un
garde-fou **en code**, vérifiable et testé — pas une consigne dans un prompt que le modèle
pourrait ignorer. C'est ce qui rend le mode de défaillance du système "gracieusement faux" (se
trompe de catégorie, mais ne ment jamais avec assurance sur un fait) plutôt que "confiant et faux".

**Écart stack assumé** : Router et Generator utilisent le même modèle (`phi4-mini:3.8b`) —
`gemma4:12b` testé, >2min sans réponse sur cette machine sans GPU. Un modèle 3× plus gros peut
devenir totalement impraticable pour un usage interactif, pas juste "un peu plus lent".

---

## 7. Les deux flux de logs

**Log technique** (debug/erreurs) vs **audit trail** (qui a coché/validé/créé quoi, quand) —
séparés parce qu'un audit de conformité (SMSI) ne doit pas être noyé dans du bruit technique.

## 8. Traces de flux — comment tout s'articule

**Chat documentaire** : `EventSource` (BFF) → `ChatController` (JWT du cookie) → **Router** →
si `DOCUMENTAIRE` : pipeline RAG (§5) → 0 résultat pertinent ? refus sans Generator (§6) → sinon
**Generator** en streaming, frame `fragment` par fragment, frame finale `termine` (sourcée +
sources).

**Circuit de validation d'un template** : RH propose (`Brouillon`) → Admin/Qualité vérifie →
Admin/Qualité approuve (`Approuve`, figé) — **seul un template `Approuve` peut instancier un
`WorkflowInstance`**, contrainte vérifiée par le use case, pas juste documentée.

**Onboarding d'un collaborateur** : RH crée une fiche (Poste, Pôle, Contrat, Date) →
`ResoudreReferentielItems` calcule les items attendus (**Poste × Pôle × Contrat** contre le
template approuvé) → `WorkflowInstance` + `ChecklistItem[]` persistés → items cochés
indépendamment (audit trail) → clôture puis archivage (lecture seule définitive).

## 9. Déploiement — qui tourne où

Docker Compose, 4 services + Ollama natif (jamais conteneurisé — décision explicite, accès à un
second Ollama d'entreprise) :

| Service | Port hôte | Rôle |
|---|---|---|
| `sqlserver` | 1433 | Entités métier relationnelles |
| `qdrant` | 6333 | Index vectoriel RAG |
| `api` | 5080 → 8080 | Backend .NET, migrations EF Core auto-appliquées |
| `frontend` | 3000 | Next.js, BFF |
| Ollama | 11434 | Natif hôte ; rejoint via `host.docker.internal` |

## 10. Ce qui reste un chantier ouvert

- **Routeur** : ~27% de mauvais classement — connu, mis de côté volontairement.
- **3 cas particuliers métier** (mutation inter-pôle, annulation/suspension, pôle vacant) — pas
  encore de use case dédié.
- **Endpoints de lecture/liste** — n'existent pas encore, seule l'écriture était priorisée.
