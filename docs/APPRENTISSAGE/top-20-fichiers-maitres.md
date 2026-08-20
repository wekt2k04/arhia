# Top 20 fichiers maîtres — guide de lecture fichier par fichier

*Complète `principal.md` (qui explique les CONCEPTS, à mémoriser) sans le remplacer : ce
document liste les 20 fichiers SOURCE à ouvrir toi-même, un par un, dans cet ordre, chacun avec
ce qu'on y trouve (méthodes, syntaxe piégeuse, variables clés) + un mini-extrait du passage le
plus important. Objectif : que tu puisses ouvrir chaque fichier dans l'IDE en le lisant et
retrouver immédiatement ce dont il est question.*

**Pondération volontaire** : 10 des 20 fichiers couvrent le pipeline RAG + l'orchestration IA
(priorité demandée), les 10 autres couvrent le socle (architecture/sécurité/domaine),
déploiement et flux temps réel/workflows — pour ne rien laisser de côté.

## Ordre de lecture conseillé

| # | Fichier | Rôle en une ligne |
|---|---|---|
| 1 | `src/Agirh.Api/Program.cs` | Composition root — câble chaque port à son adaptateur |
| 2 | `src/Agirh.Core/Security/RbacMatrix.cs` | Qui a le droit de faire quoi (3 rôles) |
| 3 | `src/Agirh.Core/Security/DepartmentScopeGuard.cs` | Portée département, vérifiée après RBAC |
| 4 | `src/Agirh.Domain/Entities/WorkflowTemplate.cs` | Machine à états du circuit de validation |
| 5 | `src/Agirh.Infrastructure/Rag/MarkdownChunker.cs` | Phase 1 RAG — découpage structurel + recouvrement |
| 6 | `src/Agirh.Infrastructure/Rag/XlmRobertaTokenizer.cs` | Tokenisation SentencePiece, correction d'espace d'IDs |
| 7 | `src/Agirh.Infrastructure/Rag/OnnxEmbeddingAdapter.cs` | Phase 2 RAG — texte → vecteur 768d |
| 8 | `src/Agirh.Infrastructure/Rag/QdrantVectorSearchAdapter.cs` | Phase 3 RAG — indexation + recherche ANN |
| 9 | `src/Agirh.Infrastructure/Rag/OnnxRerankerAdapter.cs` | Phase 4 RAG — reranking cross-encodeur |
| 10 | `src/Agirh.Core/UseCases/IngestCorpusUseCase.cs` | Orchestre chunking → embedding → indexation |
| 11 | `src/Agirh.Infrastructure/Llm/OllamaClient.cs` | Client HTTP bas niveau vers Ollama (sync + streaming) |
| 12 | `src/Agirh.Infrastructure/Llm/OllamaRouterAdapter.cs` | Router — classification d'intention fail-safe |
| 13 | `src/Agirh.Infrastructure/Llm/OllamaGeneratorAdapter.cs` | Generator — écrit la réponse finale |
| 14 | `src/Agirh.Core/UseCases/AnswerConversationUseCase.cs` | Orchestrateur central + garde-fou anti-hallucination |
| 15 | `docker-compose.yml` | 4 services, healthchecks, Ollama natif hors conteneur |
| 16 | `frontend/next.config.ts` | Le piège `compress: false` (streaming SSE) |
| 17 | `src/Agirh.Api/Controllers/ChatController.cs` | Endpoint SSE qui déclenche tout le pipeline conversationnel |
| 18 | `frontend/app/chat/chat-widget.tsx` | Consommation `EventSource` côté navigateur |
| 19 | `src/Agirh.Api/Controllers/TemplateController.cs` | 4 endpoints = 4 transitions du circuit de validation |
| 20 | `src/Agirh.Core/UseCases/InstantiateWorkflowUseCase.cs` | Le flux d'onboarding de bout en bout |

---

## A. Fondations, architecture, sécurité

### 1. `src/Agirh.Api/Program.cs`
*Composition root — seul endroit du projet qui sait quel adaptateur concret se cache derrière
chaque port.*

- `WebApplication.CreateBuilder(args)` : style ASP.NET Core minimal (pas de `Startup.cs`
  séparé) — tout est dans ce seul fichier, de haut en bas dans l'ordre d'exécution réel.
- Chaque port Core (`IEmbeddingPort`, `IVectorSearchPort`, `ILlmRouterPort`...) est enregistré
  avec une **factory lambda** : `AddSingleton<IPort>(sp => new AdaptateurConcret(...))` — permet
  de construire l'adaptateur avec des dépendances résolues à la main (chemins de fichiers,
  config) plutôt qu'auto-câblées.
- `Ollama:RouterModel` / `Ollama:GeneratorModel` lus via `builder.Configuration[...] ?? "phi4-mini:3.8b"`
  : modèle changeable sans recompiler (bascule vers le serveur Ollama d'entreprise).
- `FindRepoRoot` (fonction locale tout en bas du fichier) : remonte les dossiers parents jusqu'à
  trouver `Agirh.sln`, pour localiser `rag/models/` et `rag/corpus/` peu importe le répertoire de
  travail depuis lequel l'Api démarre.
- `scope.ServiceProvider.GetRequiredService<AgirhDbContext>().Database.Migrate();` : applique les
  migrations EF Core en attente au démarrage — idempotent, pas d'étape manuelle requise.
- Ordre du pipeline HTTP (compte réellement) : `UseHttpsRedirection` → `UseAuthentication` →
  `UseAuthorization` → `MapControllers`.

```csharp
// lignes 89-95 : pattern factory lambda répété pour chaque port RAG
builder.Services.AddSingleton<IEmbeddingPort>(_ => new OnnxEmbeddingAdapter(
    Path.Combine(embeddingModelsDir, "model_quantized.onnx"),
    Path.Combine(embeddingModelsDir, "sentencepiece.bpe.model")));

builder.Services.AddSingleton<IRerankerPort>(_ => new OnnxRerankerAdapter(
    Path.Combine(rerankerModelsDir, "model_quantized.onnx"),
    Path.Combine(rerankerModelsDir, "sentencepiece.bpe.model")));
```

### 2. `src/Agirh.Core/Security/RbacMatrix.cs`
*La totalité de la logique RBAC tient dans un dictionnaire figé + une ligne.*

- `static class`, un seul champ `Default` : `Dictionary<ResourceAction, IReadOnlySet<RoleType>>`
  — la matrice complète (12 actions → rôles autorisés) est une constante en code, pas des données
  en base (donc testable trivialement, jamais désynchronisée entre code et données).
- `IsAuthorized(role, action)` : `TryGetValue(...) && authorizedRoles.Contains(role)` — une seule
  expression, toute la vérification RBAC.
- `Roles(params RoleType[] roles) => roles.ToHashSet();` : helper `params` + méthode
  expression-bodied, juste pour écrire `Roles(RoleType.HR)` au lieu de `new HashSet<RoleType> { RoleType.HR }`.

```csharp
// lignes 9-21 : syntaxe d'indexeur pour initialiser un dictionnaire en une expression
new Dictionary<ResourceAction, IReadOnlySet<RoleType>>
{
    [ResourceAction.EmployeeCreate] = Roles(RoleType.HR),
    [ResourceAction.TemplateVerify] = Roles(RoleType.QualityAdmin),
    [ResourceAction.WorkflowInstanceRead] = Roles(RoleType.Employee, RoleType.HR, RoleType.QualityAdmin),
    // ... 9 autres entrées
};
```

### 3. `src/Agirh.Core/Security/DepartmentScopeGuard.cs`
*Deuxième porte, **séparée** de RBAC et vérifiée après elle : RBAC dit "ce rôle peut faire
cette action en général", ce fichier dit "sur CETTE cible précise".*

- Deux méthodes statiques seulement : `CanAccessDepartment` et `CanAccessEmployee`, chacune un
  `switch` **expression** (pas `switch` statement) sur `actor.Role`.
- `QualityAdmin` → toujours vrai (portée globale). `HR` → vrai seulement si même
  `DepartmentId`. `Employee` → vrai seulement sur son propre dossier (`CanAccessEmployee`
  uniquement — un Employee n'a jamais de portée sur un département entier, absent de
  `CanAccessDepartment` donc `_ => false`).
- Appelé APRÈS `RbacMatrix.IsAuthorized` dans les use cases (ex. `InstantiateWorkflowUseCase`,
  entrée 20) — jamais avant, jamais à sa place.

```csharp
// lignes 8-16
public static bool CanAccessDepartment(UserAccount actor, Guid targetDepartmentId)
{
    return actor.Role switch
    {
        RoleType.QualityAdmin => true,
        RoleType.HR => actor.DepartmentId == targetDepartmentId,
        _ => false
    };
}
```

### 4. `src/Agirh.Domain/Entities/WorkflowTemplate.cs`
*Entité pure (zéro dépendance NuGet) — machine à états du circuit de validation qualité.*

- États (`TemplateStatus`) : `Draft` → `InReview` → `Approved`/`Rejected`. Chaque méthode
  (`Submit`/`Verify`/`Approve`/`Reject`) vérifie le statut courant AVANT de transitionner
  (`InvalidOperationException` sinon) — la machine à états est imposée en code, pas juste
  documentée dans un commentaire.
- Constructeur public : valide tout (id non vide, au moins une section...) — impossible de
  construire un objet dans un état invalide. Un second constructeur **privé** (ligne 47, sans
  validation) existe séparément pour l'ORM/tests — jamais utilisé par le code applicatif.
- Règles de séparation des rôles encodées ici : `verifierId == AuthorId` refusé (le rédacteur ne
  peut pas vérifier son propre template), `approverId == VerifierId` refusé (vérificateur ≠
  approbateur).
- `ResolveApplicableItems(ContractType)` : `_sections.SelectMany(s => s.Items).Where(i =>
  i.IsApplicableFor(contractType))` — aplatit toutes les sections en une liste d'items filtrés
  par type de contrat (utilisé par `InstantiateWorkflowUseCase`, entrée 20).

```csharp
// lignes 77-90 : la transition la plus contrainte (3 vérifications avant d'agir)
public void Approve(Guid approverId)
{
    if (Status != TemplateStatus.InReview) throw new InvalidOperationException(/*...*/);
    if (VerifierId is null) throw new InvalidOperationException("Le template doit être vérifié avant d'être approuvé.");
    if (approverId == VerifierId) throw new InvalidOperationException("L'approbateur doit être distinct du vérificateur.");
    if (approverId == AuthorId) throw new InvalidOperationException("Le rédacteur ne peut pas approuver son propre template.");

    ApproverId = approverId;
    Status = TemplateStatus.Approved;
}
```

---

## B. Pipeline RAG — où chercher l'information

### 5. `src/Agirh.Infrastructure/Rag/MarkdownChunker.cs`
*Phase 1 : découpe un document Markdown en chunks, par structure (titres) et non par taille
fixe aveugle.*

- `RawChunk` : `sealed record` (TitlePath, Content, TokenCount) — type immuable, égalité
  structurelle générée automatiquement par le compilateur.
- `Func<string, int> _countTokens` injecté au constructeur — le chunker ne connaît PAS le
  tokenizer concret, juste "une fonction qui compte des tokens" (découplage volontaire).
- `ExtractSections` : parcourt le texte ligne par ligne, regex `^(#{1,6})\s+(.*)$` sur chaque
  ligne, maintient une pile `titleStack` pour reconstruire le chemin `H1 > H2 > H3`.
- `ChunkLongSection` : ne s'active que si une section dépasse `_maxTokensPerChunk` (défaut 400) ;
  fonction locale `EmitSubChunk()` (closure sur `currentParagraphs`) + calcul du recouvrement
  `Math.Max(1, (int)(currentParagraphs.Count * _overlapRatio))` (défaut 15%).
- Voisin direct : `MarkdownChunkerAdapter.cs` implémente `IDocumentChunkerPort` en enveloppant
  cette classe + génère les IDs de chunk (`DocumentChunk.ComputeId`) — ce fichier-ci reste pur,
  sans dépendance au port Core.

```csharp
// lignes 30-53 : logique principale — sous-découpe seulement si nécessaire
public IReadOnlyList<RawChunk> Chunk(string markdown)
{
    var result = new List<RawChunk>();
    foreach (var section in ExtractSections(markdown))
    {
        var textWithTitle = ComposeText(section.TitlePath, section.Body);
        var tokenCount = _countTokens(textWithTitle);
        if (tokenCount <= _maxTokensPerChunk)
            result.Add(new RawChunk(section.TitlePath, textWithTitle, tokenCount));
        else
            result.AddRange(ChunkLongSection(section));
    }
    return result;
}
```

### 6. `src/Agirh.Infrastructure/Rag/XlmRobertaTokenizer.cs`
*Le fichier le plus délicat du pipeline RAG — corrige un décalage d'indices entre deux
vocabulaires.*

- Problème réel qu'il résout : `SentencePieceTokenizer` (Microsoft.ML.Tokenizers) rend des IDs
  dans l'espace SentencePiece brut (unk=0, bos=1, eos=2), mais les poids ONNX publiés attendent
  l'espace Hugging Face (bos=0, pad=1, eos=2, unk=3) — décalage vérifié empiriquement contre
  `tokenizer.json`. `FixToHuggingFaceSpace` convertit chaque ID.
- `EncodeToHuggingFaceIds(text)` : usage simple, pour l'embedding (un seul texte).
- `EncodePairToHuggingFaceIds(textA, textB)` : encode une PAIRE pour le cross-encoder de
  reranking, format RoBERTa `<s> A </s></s> B </s>` — le double `</s>` entre les deux textes est
  la convention de la famille RoBERTa, pas une erreur.
- Syntaxe à noter : `switch` **expression** avec des littéraux entiers comme patterns.

```csharp
// lignes 79-85 : switch expression sur valeurs entières + cas "sinon" arithmétique
private static long FixToHuggingFaceSpace(int rawSentencePieceId) => rawSentencePieceId switch
{
    0 => UnkHuggingFace,
    1 => BosHuggingFace,
    2 => EosHuggingFace,
    _ => rawSentencePieceId + 1
};
```

### 7. `src/Agirh.Infrastructure/Rag/OnnxEmbeddingAdapter.cs`
*Phase 2 : texte → vecteur de 768 dimensions, via ONNX Runtime .NET pur (pas d'appel Ollama).*

- `InferenceSession` (Microsoft.ML.OnnxRuntime) chargé une seule fois dans le constructeur,
  réutilisé à chaque appel — coûteux à créer, pas à réutiliser.
- `DenseTensor<long>(new[] { 1, length })` : tenseur 2D, le `1` est la taille de batch (toujours
  1 ici — jamais de traitement par lot dans ce projet).
- `Dimension { get; } = 768` : propriété publique vérifiable, lue par `Program.cs` pour
  configurer Qdrant avec la bonne taille de vecteur — un mismatch serait détecté, pas silencieux.
- `MeanPoolAndNormalize` : boucle explicite (pas de LINQ, performance) — moyenne les embeddings
  de chaque token (mean pooling) puis divise par la norme L2 (`MathF.Sqrt(embedding.Sum(v => v *
  v))`), nécessaire pour que la similarité cosinus dans Qdrant soit bien bornée entre -1 et 1.

```csharp
// lignes 64-69 : normalisation L2 après le mean pooling
var norm = MathF.Sqrt(embedding.Sum(v => v * v));
if (norm > 0)
{
    for (var d = 0; d < Dimension; d++)
        embedding[d] /= norm;
}
```

### 8. `src/Agirh.Infrastructure/Rag/QdrantVectorSearchAdapter.cs`
*Phase 3 : indexation et recherche ANN (Approximate Nearest Neighbor) dans Qdrant.*

- `CollectionName = "agirh-corpus"` : constante privée, un seul nom codé en dur, une seule
  collection dans tout le projet.
- `PrepareAsync` : idempotent — crée la collection seulement si `CollectionExistsAsync` répond
  faux, avec `Distance.Cosine` fixée à la création (ne change jamais après).
- `IndexAsync` : `PointStruct` + `point.Payload.Add(...)` — Qdrant stocke le vecteur ET des
  métadonnées arbitraires (documentSource, chunkIndex, titlePath, content) côte à côte,
  récupérables directement après une recherche, sans requête séparée vers une autre base.
- `SearchAsync` : `_client.QueryAsync(..., limit: topK, payloadSelector: true)` — reconstruit
  chaque `DocumentChunk` depuis le payload retourné, `r.Score` = similarité cosinus.

```csharp
// lignes 32-45 : indexation d'un chunk avec ses métadonnées
var point = new PointStruct { Id = chunk.Id, Vectors = vector };
point.Payload.Add("documentSource", chunk.DocumentSource);
point.Payload.Add("chunkIndex", (long)chunk.ChunkIndex);
point.Payload.Add("titlePath", chunk.TitlePath);
point.Payload.Add("content", chunk.Content);
await _client.UpsertAsync(CollectionName, new[] { point }, cancellationToken: ct);
```

### 9. `src/Agirh.Infrastructure/Rag/OnnxRerankerAdapter.cs`
*Phase 4 (obligatoire) : reranking cross-encodeur — la phase la plus déterminante pour la
qualité des réponses.*

- Différence avec l'embedding (bi-encodeur, phase 2) : ici la PAIRE requête+document est encodée
  **ensemble**, en une seule passe ONNX — plus lent, nettement plus précis. D'où l'architecture
  en deux étages (bi-encodeur pour réduire vite le corpus, cross-encodeur pour réordonner
  finement les survivants).
- `c with { Score = ComputeScore(...) }` : syntaxe `with` sur un `record` — crée une COPIE du
  chunk avec juste `Score` modifié (implique que `DocumentChunk` est un `record`, pas une
  `class`, sinon `with` ne compilerait pas).
- Un seul logit en sortie (`onnxResults.First(r => r.Name == "logits").AsTensor<float>()[0,
  0]`) : le modèle a une seule tête de sortie (score de pertinence), pas une classification
  multi-classes.
- `Sigmoid(logit)` : `1f / (1f + MathF.Exp(-x))` — borne le logit brut (non borné, peut être
  négatif) entre 0 et 1.

```csharp
// lignes 32-38 : réordonnancement en une expression LINQ
var results = candidates
    .Select(c => c with { Score = ComputeScore(query, c.Content) })
    .OrderByDescending(c => c.Score)
    .ToList();
```

### 10. `src/Agirh.Core/UseCases/IngestCorpusUseCase.cs`
*Orchestrateur pur qui enchaîne les phases 1→2→3 à l'ingestion du corpus documentaire.*

- Ne connaît que 3 ports (`IDocumentChunkerPort`, `IEmbeddingPort`, `IVectorSearchPort`), jamais
  leurs implémentations concrètes — testable entièrement avec des mocks (voir
  `tests/Agirh.Tests/Rag/`).
- RBAC vérifié EN PREMIER (`RbacMatrix.IsAuthorized(actor.Role, ResourceAction.CorpusIngest)`),
  avant tout accès I/O — réindexer le corpus change ce que l'agent conversationnel considère
  comme source de vérité, réservé à Admin/Qualité.
- Double `foreach` imbriqué : documents → chunks de chaque document → (embedding + indexation)
  par chunk, incrémente `totalChunks` retourné à l'appelant.
- `IReadOnlyDictionary<string, string> documents` : nom de fichier → contenu Markdown déjà lu —
  ce use case ne touche jamais le système de fichiers lui-même (c'est `AdminController` qui lit
  `rag/corpus/`).

```csharp
// lignes 32-47 : RBAC d'abord, puis la boucle d'ingestion
if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.CorpusIngest))
    throw new AccessDeniedException("Seul un compte Admin/Qualité peut réindexer le corpus documentaire.");

await _vectorSearch.PrepareAsync(ct);
foreach (var (documentName, markdown) in documents)
{
    var chunks = _chunker.Chunk(documentName, markdown);
    foreach (var chunk in chunks)
    {
        var vector = await _embedding.GenerateEmbeddingAsync(chunk.Content, ct);
        await _vectorSearch.IndexAsync(chunk, vector, ct);
        totalChunks++;
    }
}
```

---

## C. Orchestration IA conversationnelle — quoi faire de la question

### 11. `src/Agirh.Infrastructure/Llm/OllamaClient.cs`
*Client HTTP bas niveau, partagé par le Router ET le Generator — un seul point de contact
réseau vers Ollama.*

- `GenerateAsync` (mode sync, `stream: false`) : retourne `string?`, `null` en cas d'échec réseau
  (`catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)`) — jamais
  d'exception qui remonte jusqu'à l'appelant.
- `GenerateStreamAsync` : `async IAsyncEnumerable<string>` + `[EnumeratorCancellation]` — syntaxe
  C# pour un générateur asynchrone streamé (`yield return` à l'intérieur d'une méthode `async
  IAsyncEnumerable<T>`).
- **Le détail le plus important du fichier** : `HttpCompletionOption.ResponseHeadersRead`. Sans
  ce flag, `HttpClient` attend la réponse complète avant de la rendre disponible — annulerait
  tout l'intérêt du streaming (même piège que `compress: false`, entrée 16, côté client cette
  fois côté serveur .NET).
- Parsing NDJSON ligne par ligne (`reader.ReadLineAsync()`), une frame JSON par ligne, `Done ==
  true` termine le flux (`yield break`).

```csharp
// ligne 66 : le flag qui rend le streaming réellement progressif
httpResponse = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
```

### 12. `src/Agirh.Infrastructure/Llm/OllamaRouterAdapter.cs`
*Le Router — classe la question en 3 catégories avant tout le reste. Fail-safe, pas
fail-open : conçu pour ne jamais ouvrir l'accès par erreur.*

- `SystemPrompt` : `const string` en **raw string literal** `"""..."""` (syntaxe C# 11+) — texte
  multi-ligne sans avoir à échapper les guillemets internes.
- 3 catégories fermées, vocabulaire français volontairement non traduit (contrat de prompt
  calibré empiriquement avec le modèle) : `DOCUMENTAIRE` / `STATUT_DOSSIER` / `HORS_PERIMETRE`.
- `ParseIntent` ne fait JAMAIS confiance à la sortie brute : `normalized.Contains("STATUT_DOSSIER")`
  (pas `==` — le modèle peut ajouter du texte autour du mot attendu), et tout ce qui ne matche ni
  l'un ni l'autre retombe sur `ConversationIntent.OutOfScope` par défaut — un flou ou une panne du
  LLM ne peut jamais accidentellement élargir l'accès.
- `_model` configurable via le constructeur (défaut `"phi4-mini:3.8b"`).
- Limite connue et assumée (commentaire dans le fichier) : ~27% de mauvais classement mesuré sur
  le jeu de test — doubler les exemples few-shot n'a eu aucun effet mesurable.

```csharp
// lignes 61-72 : parsing défensif — jamais de confiance aveugle dans la sortie du LLM
private static ConversationIntent ParseIntent(string? rawResponse)
{
    var normalized = (rawResponse ?? string.Empty).Trim().ToUpperInvariant();
    if (normalized.Contains("STATUT_DOSSIER")) return ConversationIntent.CaseStatus;
    if (normalized.Contains("DOCUMENTAIRE")) return ConversationIntent.DocumentaryQuestion;
    return ConversationIntent.OutOfScope; // vide, timeout, ou mot halluciné : jamais un accès élargi
}
```

### 13. `src/Agirh.Infrastructure/Llm/OllamaGeneratorAdapter.cs`
*Le Generator — écrit la réponse finale. Le plus court des 3 fichiers LLM.*

- Deux méthodes symétriques : `GenerateResponseAsync` (sync, réponse complète) et
  `GenerateResponseStreamingAsync` (`IAsyncEnumerable<string>`, pour le SSE).
- `MessageIndisponible` : message de repli constant si Ollama ne répond rien — l'utilisateur ne
  voit jamais une réponse vide.
- Version streaming : `receivedAtLeastOneFragment` (bool) traque si au moins un fragment est
  arrivé pendant le `await foreach` ; si le flux Ollama se termine sans rien avoir émis, le
  message de repli est yield APRÈS coup (on ne peut savoir "rien n'est arrivé" qu'une fois le
  flux épuisé).

```csharp
// lignes 33-46 : détection après-coup d'un flux vide
public async IAsyncEnumerable<string> GenerateResponseStreamingAsync(
    string systemPrompt, string question, [EnumeratorCancellation] CancellationToken ct = default)
{
    var receivedAtLeastOneFragment = false;
    await foreach (var fragment in _client.GenerateStreamAsync(_model, systemPrompt, question, ct))
    {
        receivedAtLeastOneFragment = true;
        yield return fragment;
    }
    if (!receivedAtLeastOneFragment)
        yield return MessageIndisponible;
}
```

### 14. `src/Agirh.Core/UseCases/AnswerConversationUseCase.cs`
*Le fichier le plus dense du projet — orchestrateur central de toute la conversation. Contient
le garde-fou anti-hallucination, probablement le code le plus important du dépôt.*

- Deux points d'entrée publics partagent la même préparation :
  `PrepareDocumentaryContextAsync` est le **point unique de vérité** (embedding → recherche
  Qdrant → reranking → filtrage) — `ExecuteAsync` (réponse complète) et `ExecuteStreamingAsync`
  (`IAsyncEnumerable<ConversationEvent>`, pour le SSE) ne peuvent donc jamais diverger.
- **Le garde-fou tient en deux portes de sortie anticipée**, toutes deux dans
  `PrepareDocumentaryContextAsync` : `candidates.Count == 0` (rien trouvé dans Qdrant) et, après
  reranking + filtrage par seuil, `best.Count == 0`. Dans les deux cas le Generator n'est
  **jamais appelé** — refus en code, pas une consigne de prompt que le modèle pourrait ignorer.
- `GeneratorRefusalIndicators` : tableau de plusieurs formulations (pas une seule phrase) — le
  générateur ne reprend pas toujours la formule exacte imposée, donc `IsGeneratorRefusal` teste
  plusieurs variantes (`.Contains(..., StringComparison.OrdinalIgnoreCase)`).
- `readonly record struct DocumentaryPreparation(...)` : type de retour interne combinant
  plusieurs valeurs — `record struct` = valeur (copié), contrairement à un `record class` (référence).
- Constantes clés : `TopKSearch = 5`, `TopKAfterReranking = 3`, `MinimumRelevanceThreshold =
  0.01f` — le seuil est bas volontairement (un score élevé mesure la proximité thématique, pas
  "la réponse est présente" ; c'est le texte réellement généré qui est relu ensuite, pas le score
  seul, pour décider si la réponse est sourcée).
- `AnswerCaseStatusAsync` : vérifie `DepartmentScopeGuard.CanAccessEmployee` avant de lire un
  dossier — même pattern rôle-puis-portée que dans `InstantiateWorkflowUseCase` (entrée 20).

```csharp
// lignes 184-194 : les deux portes de sortie anticipée, avant tout appel au Generator
var candidates = await _vectorSearch.SearchAsync(queryVector, TopKSearch, ct);
if (candidates.Count == 0)
    return new DocumentaryPreparation(false, null, Array.Empty<DocumentChunk>());

var reranked = await _reranker.RerankAsync(question, candidates, ct);
var best = reranked.Where(c => c.Score >= MinimumRelevanceThreshold).Take(TopKAfterReranking).ToList();
if (best.Count == 0)
    return new DocumentaryPreparation(false, null, Array.Empty<DocumentChunk>());
```

---

## D. Dockerisation & déploiement

### 15. `docker-compose.yml`
*4 services orchestrés ; Ollama volontairement PAS conteneurisé.*

- Services : `sqlserver`, `qdrant`, `api`, `frontend`. Ollama tourne nativement sur la machine
  hôte (déjà installé, déjà utilisé en dev) — pas un oubli, une décision explicite documentée en
  commentaire dans le fichier.
- `depends_on` avec conditions **mixtes**, pas une règle uniforme : `sqlserver` a
  `condition: service_healthy` (attend le `healthcheck` `sqlcmd`), `qdrant` a seulement
  `condition: service_started` (Qdrant n'a pas de healthcheck défini ici).
- `host.docker.internal` : nom spécial résolu par Docker Desktop vers l'hôte, utilisé par le
  conteneur `api` pour joindre Ollama (`Ollama__BaseUrl`). `extra_hosts` ajouté pour la
  portabilité vers Docker Engine Linux sans Docker Desktop (sans effet ici).
- Convention ASP.NET Core : double underscore dans une variable d'env (`Jwt__TokenLifetimeMinutes`)
  = mappé vers une clé de config hiérarchique (`Jwt:TokenLifetimeMinutes` dans le C#).
- `rag/models` et `rag/corpus` montés en lecture seule (`:ro`) depuis l'hôte plutôt que copiés
  dans l'image (~850 Mo de modèles).

```yaml
# lignes 41-45 : depends_on à conditions mixtes
depends_on:
  sqlserver:
    condition: service_healthy
  qdrant:
    condition: service_started
```

### 16. `frontend/next.config.ts`
*13 lignes, mais documente le piège le plus instructif du projet côté frontend.*

- `compress: false` : la compression HTTP intégrée de Next.js bufferise TOUTE la réponse avant
  de l'envoyer — pensé pour des réponses classiques, incompatible avec un flux progressif SSE
  (`app/api/chat/ask`, `app/api/notifications/stream`). Sans ce flag, rien n'arrive au client
  avant la fin complète du flux côté `Agirh.Api` — vérifié empiriquement, pas une supposition.
- `devIndicators: false` : désactive le badge de dev Next.js (gênait les captures d'écran/démos)
  — sans lien avec le streaming, juste une préférence.
- Illustre un piège général à retenir : une optimisation par défaut, pensée pour le cas
  classique, peut casser silencieusement un cas d'usage différent, sans lever d'erreur explicite.

```typescript
// lignes 3-11 : le fichier dans son intégralité
const nextConfig: NextConfig = {
  compress: false,
  devIndicators: false,
};
```

---

## E. Flux temps réel & workflows métier

### 17. `src/Agirh.Api/Controllers/ChatController.cs`
*Endpoint SSE qui déclenche tout le pipeline conversationnel (Router → RAG → Generator).*

- `[HttpGet("ask")]` : SSE via **GET**, pas POST — la question part en paramètre d'URL
  (`?question=...`), contrainte imposée par l'API navigateur `EventSource` qui ne supporte que
  GET.
- Headers manuels avant d'écrire quoi que ce soit : `Content-Type: text/event-stream`,
  `Cache-Control: no-cache`, `X-Accel-Buffering: no` (désactive le buffering d'un éventuel
  reverse proxy Nginx en amont).
- `WriteEventAsync` : `switch` **expression** avec pattern matching sur le type concret de
  `ConversationEvent` (`TextFragment f => (...)`, `ResponseCompleted r => (...)`) — possible
  parce que `ConversationEvent` est un `abstract record` et `TextFragment`/`ResponseCompleted`
  sont des `sealed record` qui en héritent (`Core/Ports/ConversationEvent.cs`). Construit
  `event: {type}\ndata: {json}\n\n` (format SSE standard) puis `FlushAsync` immédiatement — sans
  flush explicite, .NET bufferiserait aussi côté serveur.
- `catch (AccessDeniedException)` : traduit en message conversationnel dans le flux plutôt
  qu'une erreur HTTP — un refus d'accès reste une réponse "normale" du point de vue de
  l'utilisateur du chat.
- `catch (OperationCanceledException)` : capturé silencieusement — un client qui ferme l'onglet
  annule le `CancellationToken`, ce n'est pas une erreur applicative.

```csharp
// lignes 70-75 : pattern matching sur les sous-types d'un record abstrait
var (type, data) = conversationEvent switch
{
    TextFragment f => ("fragment", (object)new { text = f.Text }),
    ResponseCompleted r => ("done", new { sourced = r.Sourced, sources = r.Sources }),
    _ => throw new InvalidOperationException(/*...*/)
};
```

### 18. `frontend/app/chat/chat-widget.tsx`
*Consommation `EventSource` côté navigateur — ferme la boucle ouverte par ChatController.*

- `"use client"` : directive Next.js App Router — ce composant s'exécute côté navigateur
  (nécessaire pour `useState`/`EventSource`), contrairement aux Server Components par défaut de
  l'App Router.
- `new EventSource(url)` ouvre la connexion ; `source.addEventListener("fragment", ...)` et
  `("done", ...)` — noms d'événements qui doivent correspondre **exactement** à ceux écrits par
  `ChatController.WriteEventAsync` (entrée 17) côté serveur, sinon rien ne s'affiche silencieusement.
- `mettreAJourDernierMessage` : closure qui copie le tableau `messages` et remplace uniquement le
  dernier élément — accumule le texte fragment par fragment sans reconstruire tout l'historique
  affiché.
- `eventSourceRef` (`useRef<EventSource | null>`) : garde une référence à la connexion active
  pour pouvoir `.close()` une requête en cours si une nouvelle question part avant que la
  précédente soit terminée.
- `role="status" aria-live="polite"` + classe `sr-only` : accessibilité — annonce vocalement
  "l'assistant est en train de répondre" pour un lecteur d'écran, invisible visuellement.

```typescript
// lignes 50-60 : écoute des deux événements SSE nommés, doit matcher le serveur au caractère près
source.addEventListener("fragment", (evt) => {
  const donnees = JSON.parse((evt as MessageEvent).data) as { text: string };
  mettreAJourDernierMessage((m) => ({ ...m, text: m.text + donnees.text }));
});

source.addEventListener("done", (evt) => {
  const donnees = JSON.parse((evt as MessageEvent).data) as { sourced: boolean; sources: string[] };
  mettreAJourDernierMessage((m) => ({ ...m, sourced: donnees.sourced, sources: donnees.sources }));
  source.close();
  setEnCours(false);
});
```

### 19. `src/Agirh.Api/Controllers/TemplateController.cs`
*4 endpoints qui correspondent 1:1 aux 4 transitions de `WorkflowTemplate` (entrée 4).*

- Controller **fin** (thin controller) : aucune règle métier ici — construire les objets depuis
  le DTO → appeler le use case → traduire les exceptions en codes HTTP. Toute la logique vit dans
  `Agirh.Core/UseCases/` et dans l'entité Domain elle-même.
- Table de traduction d'exceptions, identique dans les 4 actions : `AccessDeniedException` →
  `Forbid()` (403), `InvalidOperationException` → `Conflict()` (409, ex. mauvaise transition
  d'état), `ArgumentException` → `BadRequest()` (400).
- DTOs de requête = `record` (`ProposeTemplateRequest`, `TemplateItemRequest`...) —
  désérialisés automatiquement depuis le JSON par le model binding ASP.NET Core, sans code de
  parsing manuel.
- `Propose` reconstruit les `TemplateSection`/`TemplateItem` (Domain) à partir des DTOs avec
  `Guid.NewGuid()` pour chaque identifiant — les IDs sont générés côté serveur, jamais fournis
  par le client.

```csharp
// lignes 63-74 : traduction d'exceptions vers codes HTTP, pattern répété sur les 4 endpoints
catch (AccessDeniedException) { return Forbid(); }
catch (ArgumentException ex) { return BadRequest(ex.Message); }
catch (InvalidOperationException ex) { return Conflict(ex.Message); }
```

### 20. `src/Agirh.Core/UseCases/InstantiateWorkflowUseCase.cs`
*Le flux d'onboarding de bout en bout, résumé en un seul `ExecuteAsync`.*

- Séquence complète : RBAC (`WorkflowInstantiate`) → portée
  (`DepartmentScopeGuard.CanAccessEmployee`) → dernier template **approuvé** du bon type
  (`GetLastApprovedAsync` — pas "un template", le DERNIER approuvé, contrainte héritée du circuit
  de validation, entrée 4/19) → `template.ResolveApplicableItems(employee.ContractType)` →
  construction des `ChecklistItemStatus` → persistance.
- `?? throw new InvalidOperationException(...)` : opérateur de coalescence null combiné à
  `throw` comme expression (C# 7+) — "récupère la valeur ou échoue immédiatement", évite un `if
  (x is null) { throw ... }` séparé. Utilisé deux fois (employé introuvable, aucun template
  approuvé).
- Illustre le pattern répété à l'identique dans les use cases voisins non détaillés ici
  (`CheckItemUseCase`, `CloseCaseUseCase`, `ArchiveCaseUseCase`) : une seule méthode
  `ExecuteAsync`, un seul chemin heureux, échecs explicites par exception plutôt que par valeur de
  retour ambiguë.

```csharp
// lignes 33-45 : RBAC, puis portée, puis résolution du template approuvé — dans cet ordre précis
if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstantiate))
    throw new AccessDeniedException("Seul un RH peut instancier un workflow.");

var employee = await _employees.GetByIdAsync(employeeId, ct)
    ?? throw new InvalidOperationException($"Collaborateur {employeeId} introuvable.");

if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
    throw new AccessDeniedException("Un RH ne peut instancier un workflow que pour un collaborateur de son pôle.");

var template = await _templates.GetLastApprovedAsync(type, ct)
    ?? throw new InvalidOperationException($"Aucun template approuvé pour le type {type}.");
```

---

## Et après ces 20 ?

Si tu veux aller plus loin une fois ces 20 fichiers digérés, dans cet ordre de priorité :
`src/Agirh.Domain/Entities/WorkflowInstance.cs` (le pendant "instance" de l'entrée 4),
`src/Agirh.Infrastructure/Persistence/AgirhDbContext.cs` (mapping EF Core), les fichiers
`tests/Agirh.Tests/Rag/*.cs` (montrent le pipeline RAG testé bout en bout sur le vrai corpus),
et `src/Agirh.Api/Controllers/WorkflowController.cs` (check/close/archive, même style que
l'entrée 19).
