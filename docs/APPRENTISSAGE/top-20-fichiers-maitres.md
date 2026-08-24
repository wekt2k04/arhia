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

**Trace d'exécution.** Au lancement, `WebApplication.CreateBuilder` ouvre un conteneur DI que le
reste du fichier remplit dans l'ordre : repositories EF Core, hasher et générateur JWT, puis un
use case par action métier. Vient ensuite le câblage RAG : `FindRepoRoot` remonte l'arborescence
depuis `AppContext.BaseDirectory` jusqu'à `Agirh.sln`, pour localiser `rag/models/` quel que soit
le répertoire de lancement. `IEmbeddingPort`/`IRerankerPort` sont enregistrés comme adaptateurs
ONNX concrets, construits directement avec leurs deux chemins de fichiers (pas de constructeur
sans paramètre). Conséquence en aval : `QdrantVectorSearchAdapter` (entrée 8) reçoit sa dimension
via `IEmbeddingPort.Dimension` (768, lu au runtime sur l'adaptateur déjà résolu) plutôt qu'une
constante séparée — un futur modèle d'embedding de taille différente propagerait sa dimension
sans toucher ce fichier. Une fois `app.Build()` appelé, `Database.Migrate()` s'exécute avant tout
le reste, idempotent ; puis le pipeline HTTP suit l'ordre déclaré : `UseAuthentication` peuple
`HttpContext.User` avant que `UseAuthorization` ne le lise — les inverser casserait la sécurité
sans la moindre erreur de compilation.

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

**Trace d'exécution.** Ce fichier n'est jamais appelé directement par un contrôleur : chaque use
case (`InstantiateWorkflowUseCase`, `IngestCorpusUseCase`...) commence son `ExecuteAsync` par
`RbacMatrix.IsAuthorized(actor.Role, ResourceAction.X)`, où `actor` n'est pas le rôle inscrit
dans le JWT au moment de la connexion mais le `UserAccount` réellement relu en base par
`CurrentUserAccessor.GetActorAsync` à chaque requête (seul le `NameIdentifier` du token sert à
retrouver la ligne) : un changement de rôle appliqué entre deux requêtes est donc pris en compte
immédiatement, sans reconnexion. `IsAuthorized` fait un simple `TryGetValue` sur `Default` : si
l'action demandée n'y figure pas, l'appel retourne `false` par construction — une action oubliée
dans la matrice est refusée à tout le monde, jamais accordée par défaut (fail-safe par omission,
pas fail-open). Une fois cette porte franchie, le use case appelle en général une seconde
vérification, `DepartmentScopeGuard`, qui restreint la cible précise plutôt que l'action en
général.

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

**Trace d'exécution.** Ce fichier s'appelle toujours en second, après un `RbacMatrix.IsAuthorized`
déjà passé — jamais avant, jamais à sa place (visible dans `InstantiateWorkflowUseCase.ExecuteAsync`,
entrée 20 : RBAC d'abord, `CanAccessEmployee` ensuite). La distinction entre les deux méthodes
reflète deux granularités : `CanAccessDepartment` teste l'accès à un pôle entier,
`CanAccessEmployee` teste UNE fiche précise. Un `Employee` n'apparaît que dans la seconde —
`CanAccessDepartment` n'a pas de branche `RoleType.Employee` explicite, elle retombe sur `_ =>
false` : un compte Employee n'a par construction jamais de vue sur un département entier, juste
sur son propre dossier (`actor.Id == target.UserAccountId`, une identité de compte, pas de
département). Comme `RbacMatrix`, ce garde-fou lit `actor.Role`/`actor.DepartmentId` sur le
`UserAccount` fraîchement relu en base à chaque requête : un transfert de département appliqué
entre deux requêtes change immédiatement la portée effective, sans JWT à réémettre.

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

**Trace d'exécution.** Le cycle de vie suit toujours la même trajectoire : `Draft` à la
construction, `InReview` après `Submit()`, puis `Approved`/`Rejected` après `Verify()`+`Approve()`
ou `Reject()` — jamais l'inverse, chaque méthode vérifie `Status` en première ligne et lève sinon,
la machine à états est donc imposée par le code, pas seulement documentée. `Verify`/`Approve`
encodent la séparation des rôles du circuit qualité : le rédacteur (`AuthorId`) ne peut vérifier
ni approuver son propre travail, et le vérificateur ne peut pas non plus être l'approbateur —
trois identités distinctes doivent se succéder. `ResolveApplicableItems(contractType)` relie ce
fichier à `InstantiateWorkflowUseCase` (entrée 20) : elle aplatit les sections en une liste, ne
gardant que les items dont `IsApplicableFor` répond vrai — ce qui dépend de
`TemplateItem.ApplicableContractTypes` : une collection vide s'applique à tous les contrats, non
vide restreint l'item aux types listés. Ce n'est donc jamais `WorkflowTemplate` qui décide de la
restriction, seulement chaque item individuellement. Le second constructeur, privé et sans
validation, n'existe que pour l'ORM — le code applicatif passe toujours par le constructeur
public, qui refuse tout état invalide dès l'instanciation.

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

**Trace d'exécution.** `Chunk(markdown)` prend du texte brut et rend une liste de `RawChunk` — un
type purement interne, sans identifiant ni source, juste un chemin de titres, un contenu, un
compte de tokens. `ExtractSections` fait le découpage structurel : chaque ligne `#`-préfixée
empile/dépile `titleStack` pour reconstruire un chemin `H1 > H2 > H3`, le texte suivant devenant
le corps de la section. Une section sous `_maxTokensPerChunk` (400, compté via `_countTokens`
injecté — ce fichier ignore que c'est en réalité `XlmRobertaTokenizer.CountTokens`, entrée 6)
devient un `RawChunk` tel quel ; plus longue, elle passe par `ChunkLongSection`, qui découpe
paragraphe par paragraphe en conservant un recouvrement (`_overlapRatio`, 15%) entre deux
sous-chunks. Ce fichier reste délibérément pur, sans dépendance à un identifiant de document :
c'est `MarkdownChunkerAdapter` qui enveloppe chaque `RawChunk` dans un `DocumentChunk` complet, en
zippant la liste avec son index et en calculant un ID déterministe (`DocumentChunk.ComputeId`, un
hash MD5 de `documentSource#chunkIndex`). Conséquence peu visible ici : réingérer deux fois le
même document produit les mêmes IDs de chunk, donc `IndexAsync` (entrée 8) écrase les points
existants au lieu de les dupliquer.

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

**Trace d'exécution.** Un document Markdown est d'abord découpé par `MarkdownChunker` en
plusieurs chunks, généralement limités à environ 400 tokens. Pour chaque chunk,
`SentencePieceTokenizer` le transforme en identifiants bruts, puis `XlmRobertaTokenizer` les
convertit vers l'espace d'identifiants attendu par Hugging Face. Ces identifiants sont ensuite
envoyés au modèle ONNX d'embedding, qui produit une représentation par token
(`last_hidden_state`). L'application effectue alors un mean-pooling, puis une normalisation L2,
afin d'obtenir un vecteur `float[768]` stocké dans Qdrant. Lorsqu'un utilisateur pose une
question, celle-ci suit le même processus avec `EncodeToHuggingFaceIds` : elle est transformée en
vecteur 768 et comparée aux vecteurs des chunks pour récupérer les passages les plus proches
sémantiquement. Les quelques chunks candidats sont ensuite évalués individuellement avec
`EncodePairToHuggingFaceIds`, qui construit une séquence contenant la question et le document
(`<s> question </s></s> document </s>`) ; le modèle ONNX de reranking leur attribue un score de
pertinence et les réordonne. Il n'existe donc pas d'encodeur Hugging Face séparé dans ce code : la
classe locale convertit seulement les identifiants SentencePiece, tandis que le modèle ONNX
produit réellement les représentations. Enfin, le commentaire du code parle de mean-pooling
« masqué », mais l'implémentation actuelle moyenne tous les tokens, car le `attentionMask` n'est
pas encore utilisé dans cette étape.

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

**Trace d'exécution.** `InferenceSession` est instancié une seule fois dans le constructeur et
réutilisé à chaque appel de `GenerateEmbeddingAsync` — charger un modèle ONNX est coûteux,
l'exécuter ne l'est pas, d'où le `Singleton` avec lequel `Program.cs` (entrée 1) enregistre cet
adaptateur. Chaque appel construit deux tenseurs `[1, length]` (`inputIds`, `attentionMask`) à
partir des identifiants d'`EncodeToHuggingFaceIds` (entrée 6) — le `1` est la taille de batch,
toujours 1 ici, et `attentionMask` est rempli de `1` sur toute la longueur puisqu'il n'y a jamais
de remplissage à signaler pour une séquence unique. Le graphe renvoie `last_hidden_state` (par
token) ; `MeanPoolAndNormalize` additionne ces représentations puis divise par `length`, puis par
la norme L2 du résultat — cette seconde division est ce qui rend la similarité cosinus de Qdrant
(entrée 8) bien bornée entre -1 et 1. `Dimension` (768) n'est pas qu'une information : `Program.cs`
la relit sur cet adaptateur déjà construit pour dimensionner la collection Qdrant au démarrage,
donc un changement de modèle se propagerait sans constante à modifier ailleurs.

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

**Trace d'exécution.** `PrepareAsync`, appelé par `IngestCorpusUseCase` (entrée 10) avant toute
indexation, ne crée la collection `agirh-corpus` que si `CollectionExistsAsync` répond faux, avec
`Distance.Cosine` fixée définitivement — cohérent avec la normalisation L2 faite en amont (entrée
7), la similarité cosinus n'ayant de sens stable que sur des vecteurs déjà normalisés. `IndexAsync`
stocke vecteur ET métadonnées (`documentSource`, `chunkIndex`, `titlePath`, `content`) côte à côte
dans `Payload` : une recherche renvoie directement le texte du chunk, sans requête séparée.
`UpsertAsync` — pas une insertion pure — signifie que réindexer un chunk dont l'ID existe déjà
(déterministe, entrée 5) remplace le point au lieu de le dupliquer, ce qui rend `IngestCorpusUseCase`
sûr à relancer sur un corpus inchangé. `SearchAsync` reconstruit chaque `DocumentChunk` depuis le
payload retourné ; `r.Score`, la similarité cosinus brute, n'est PAS le score final utilisé par
`AnswerConversationUseCase` (entrée 14) — il sert seulement à sélectionner les `TopKSearch` (5)
candidats avant que le reranker (entrée 9) ne recalcule un score différent sur cette short-list.

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

**Trace d'exécution.** `RerankAsync` reçoit les `DocumentChunk` renvoyés par Qdrant (entrée 8,
avec leur `Score` de similarité cosinus) et leur substitue un score différent : `c with { Score =
ComputeScore(...) }` écrase le score de similarité vectorielle par un score de pertinence
cross-encoder — même champ, deux significations selon l'étape du pipeline. `ComputeScore` encode
la PAIRE requête+document en une seule séquence via `EncodePairToHuggingFaceIds` (entrée 6),
l'envoie en une passe ONNX, puis lit un unique logit (une seule tête de sortie, pas une
classification multi-classes). Ce logit non borné est ramené dans `[0, 1]` par `Sigmoid`, l'échelle
attendue par `MinimumRelevanceThreshold` dans `AnswerConversationUseCase` (entrée 14).
Architecturalement, ce fichier est le second étage d'un pipeline à deux vitesses : le bi-encodeur
(entrée 7) encode requête et documents séparément pour une recherche rapide sur tout le corpus,
tandis que ce cross-encodeur, plus lent, ne s'applique qu'aux `TopKSearch` (5) survivants pour les
réordonner avec une bien meilleure précision.

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

**Trace d'exécution.** `ExecuteAsync` enchaîne réellement les trois phases du pipeline RAG en une
boucle : pour chaque document (nom → contenu Markdown déjà lu — ce use case ne touche jamais le
système de fichiers, c'est `AdminController` qui lit `rag/corpus/` en amont), `_chunker.Chunk`
(entrée 5) produit des `DocumentChunk` identifiés, puis pour chacun, `_embedding.GenerateEmbeddingAsync`
(entrée 7) calcule son vecteur avant que `_vectorSearch.IndexAsync` (entrée 8) ne l'indexe —
séquentiellement, jamais en parallèle ni par lot. Le use case ne connaît que les trois ports,
jamais les classes concrètes, ce qui permet de le tester entièrement avec des mocks
(`tests/Agirh.Tests/Rag/`), sans ONNX Runtime ni Qdrant réel. La vérification RBAC (`CorpusIngest`,
réservée à `QualityAdmin`) intervient avant même `PrepareAsync` : réindexer le corpus change ce que
`AnswerConversationUseCase` considère ensuite comme source de vérité documentaire pour tout le
monde, une conséquence assez large pour justifier le rôle le plus élevé plutôt que HR.

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

**Trace d'exécution.** Ce client est partagé tel quel par le Router (entrée 12) et le Generator
(entrée 13) : aucun des deux ne parle HTTP directement. `GenerateAsync` encapsule tout échec
réseau dans un simple `return null` plutôt qu'une exception — ce sont les appelants qui décident
quoi faire d'une réponse absente (le Router retombe sur `HORS_PERIMETRE`, le Generator affiche
`MessageIndisponible`), pas ce fichier. `GenerateStreamAsync` est le chemin du chat SSE : le
détail qui change tout est `HttpCompletionOption.ResponseHeadersRead` passé à `SendAsync` — sans
lui, `HttpClient` attendrait la réponse complète avant de la rendre disponible, vidant le
streaming de son intérêt côté serveur .NET, exactement le même piège que `compress: false` côté
Next.js (entrée 16) mais un cran plus tôt. Le flux est ensuite lu ligne par ligne : chaque ligne
NDJSON est désérialisée par `DeserializeFrameSafely` (qui avale silencieusement une frame corrompue
plutôt que de faire planter le flux), `Response` est renvoyé dès qu'il est non vide, et `Done ==
true` termine l'énumération — cette même frame `done` deviendra plus tard l'événement SSE `done`
de `ChatController` (entrée 17), sans être le même signal : l'un marque la fin du flux Ollama,
l'autre la fin du message conversationnel complet.

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

**Trace d'exécution.** `ClassifyAsync` est le tout premier appel LLM d'une conversation :
`AnswerConversationUseCase` (entrée 14) l'appelle avant de savoir si la question porte sur un
dossier personnel ou une politique documentaire, en envoyant la question et le `SystemPrompt`
(few-shot) au modèle configuré, puis en passant la réponse à `ParseIntent`. Ce qui rend ce fichier
fail-safe plutôt que fail-open tient dans `ParseIntent` : la réponse est testée avec `.Contains(...)`
(jamais `==`, le modèle ajoute parfois du texte autour du mot attendu), et toute réponse qui ne
contient ni `STATUT_DOSSIER` ni `DOCUMENTAIRE` — vide, `null` remonté par `OllamaClient` (entrée
11) après timeout, ou mot halluciné — retombe sur `OutOfScope`. Une panne d'Ollama à cette étape ne
provoque donc jamais un accès élargi : elle produit silencieusement la réponse la plus restrictive.
Le prompt porte une règle affinée après qu'une version plus simple ait mal classé « qui signe la
fiche de décharge ? » en question de statut personnel — et malgré ce travail, ~27% des cas restent
mal classés sur le jeu de test, limite documentée dans le fichier lui-même.

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

**Trace d'exécution.** Ce fichier est appelé en dernier dans la branche documentaire, uniquement
après qu'`AnswerConversationUseCase` (entrée 14) ait déjà décidé qu'un contexte pertinent existe —
ce n'est pas lui qui refuse de répondre, la décision est prise en amont. `GenerateResponseAsync`
est la version simple : une réponse vide ou nulle devient `MessageIndisponible`, jamais une chaîne
vide affichée à l'utilisateur. `GenerateResponseStreamingAsync` résout un problème que la version
simple n'a pas : au moment de commencer à émettre des fragments, on ignore encore si le flux sera
vide — d'où `receivedAtLeastOneFragment`, vérifiable seulement APRÈS la fin du `await foreach`. Si
aucun fragment n'est jamais arrivé, `MessageIndisponible` est émis après coup, la seule façon de le
savoir avec certitude sur un flux progressif. C'est ce texte, accumulé fragment par fragment côté
appelant, qu'`AnswerConversationUseCase` relit ensuite en entier pour vérifier s'il s'agit en
réalité d'un refus du modèle plutôt qu'une vraie réponse sourcée — ce fichier l'ignore, il se
contente de produire le texte.

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

**Trace d'exécution.** `ExecuteAsync` (et son jumeau streamé) commence toujours par
`_router.ClassifyAsync` (entrée 12) : selon l'intention, la question part vers
`AnswerDocumentaryAsync` (RAG complet), `AnswerCaseStatusAsync` (lecture d'un `WorkflowInstance`),
ou `OutOfScopeResponse` (aucun appel LLM de plus). Le garde-fou anti-hallucination tient dans
`PrepareDocumentaryContextAsync`, seule méthode qui touche Qdrant : embedding de la question
(entrée 7) → `TopKSearch` (5) candidats (entrée 8) → reranking (entrée 9) → filtrage par
`MinimumRelevanceThreshold` (0.01, bas volontairement — un score de reranking mesure une proximité
thématique, pas la certitude que la réponse précise s'y trouve) → `TopKAfterReranking` (3)
meilleurs. Si `candidates` ou `best` est vide, le Generator (entrée 13) n'est JAMAIS appelé — deux
portes de sortie dans le code, pas dans un prompt ignorable. Même appelé, sa réponse est relue par
`IsGeneratorRefusal` contre 7 formulations de refus possibles (le modèle 3.8B ne reprend pas
toujours la formule exacte imposée) : un refus déguisé renvoie `Sourced: false` malgré des chunks
pertinents. `ExecuteAsync` et sa version streamée partagent cette même préparation, donc ne peuvent
jamais diverger. Côté statut de dossier, `DepartmentScopeGuard` (entrée 3) s'applique avant toute
lecture, et sans `targetEmployeeId`, seul un acteur `Employee` obtient une résolution implicite de
son propre dossier — jamais un RH ou un Admin.

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

**Trace d'exécution.** Un `docker compose up` démarre 4 services à des rythmes différents : `api`
attend que `sqlserver` réponde `service_healthy` (le `healthcheck` sqlcmd doit réussir, pas
seulement démarrer) mais seulement `service_started` pour `qdrant`, qui n'a pas de healthcheck
défini ici — une différence de robustesse assumée, pas un oubli symétrique. Ollama est absent par
choix explicite (commenté dans le YAML) : il tourne nativement sur l'hôte, joint via
`host.docker.internal`, un nom résolu par Docker Desktop — `extra_hosts` n'a d'effet que sous
Docker Engine Linux sans Docker Desktop, sans effet ici. Les variables à double underscore
(`Jwt__TokenLifetimeMinutes`) sont la convention ASP.NET Core pour mapper vers une clé hiérarchique
(`Jwt:TokenLifetimeMinutes`), la même config que lit `Program.cs` (entrée 1) avec repli par défaut
si absente. `rag/models` et `rag/corpus` sont montés en lecture seule depuis l'hôte plutôt que
copiés dans l'image : à ~850 Mo de modèles, les copier alourdirait chaque build pour rien.

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

**Trace d'exécution.** Deux lignes de configuration, mais l'ordre des événements qu'elles
empêchent mérite d'être tracé : sans `compress: false`, Next.js bufferiserait en entier la réponse
de `/api/chat/ask` (qui relaie le SSE de `ChatController`, entrée 17) avant de l'envoyer au
navigateur — le fragment le plus rapide attendrait le dernier, annulant le streaming construit à
trois niveaux (`OllamaClient`, entrée 11 ; `ChatController.WriteEventAsync`, entrée 17 ;
`EventSource`, entrée 18). C'est le même piège que `HttpCompletionOption.ResponseHeadersRead`
(entrée 11), un étage plus haut, côté serveur Next.js plutôt que client HTTP .NET — une
optimisation par défaut pensée pour des réponses classiques qui casse silencieusement un flux
progressif. `devIndicators: false` n'a aucun rapport : simple préférence pour ne pas polluer les
captures d'écran de démo.

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

**Trace d'exécution.** `Ask` est le point d'entrée de toute conversation : `[HttpGet("ask")]`, en
GET, parce que `EventSource` (entrée 18) ne sait ouvrir une connexion SSE qu'en GET — la question
voyage en paramètre d'URL, pas dans un corps de requête. Avant d'écrire le moindre octet, la
méthode pose trois en-têtes manuellement, dont `X-Accel-Buffering: no` (effet seulement derrière un
reverse proxy Nginx, absent en dev). Le corps se résume à un `await foreach` sur
`_answerConversation.ExecuteStreamingAsync` (entrée 14) : chaque `ConversationEvent` passe par
`WriteEventAsync`, qui `switch`e sur le type concret (`TextFragment` → `fragment`,
`ResponseCompleted` → `done`) pour écrire une frame SSE puis `FlushAsync` explicitement — sans ce
flush, ASP.NET Core bufferiserait à son tour, un troisième endroit où le piège de `compress: false`
(entrée 16) pourrait se reproduire. Les noms `"fragment"`/`"done"` doivent correspondre AU
CARACTÈRE PRÈS aux `addEventListener` du widget React (entrée 18) : un décalage ne lèverait aucune
erreur, juste un silence côté navigateur. Un `AccessDeniedException` levé plus bas (via
`DepartmentScopeGuard`, entrée 14) est transformé ici en message conversationnel plutôt qu'un code
HTTP d'erreur.

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

**Trace d'exécution.** `envoyer` ajoute immédiatement deux messages (utilisateur, et un message
assistant vide à remplir progressivement) puis ouvre `new EventSource(url)` vers `/api/chat/ask` —
cette requête GET exécute côté serveur tout ce qui est décrit à l'entrée 17. Les deux
`addEventListener` (`fragment`, `done`) sont l'autre bout exact du contrat SSE de
`ChatController.WriteEventAsync` : à chaque `fragment`, `mettreAJourDernierMessage` concatène le
nouveau texte au dernier élément (`m.text + donnees.text`), donnant l'effet de texte qui s'écrit
progressivement ; à `done`, le même helper attache `sourced`/`sources` puis ferme la connexion et
redonne la main au formulaire. `eventSourceRef` gère un cas que l'utilisateur peut déclencher
lui-même : une nouvelle question envoyée pendant qu'une réponse arrive encore ferme l'ancien flux
avant d'en ouvrir un nouveau, pour ne jamais mélanger deux réponses. Le composant est `"use client"` :
sans cette directive, `useState`/`useRef`/`EventSource` ne pourraient pas s'exécuter, l'App Router
traitant tout composant par défaut comme un Server Component, incapable de maintenir une connexion
ouverte vers le navigateur.

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

**Trace d'exécution.** Les 4 endpoints (`Propose`, `Verify`, `Approve`, `Reject`) ne contiennent
aucune règle métier propre : chacun construit ses objets depuis le DTO, délègue au use case
correspondant, puis traduit l'exception en code HTTP — la même table (`AccessDeniedException` →
403, `InvalidOperationException` → 409, `ArgumentException` → 400) est répétée dans les 4 actions.
Ce n'est pas un oubli de factorisation : chaque action échoue pour une raison différente selon
l'état du `WorkflowTemplate` visé (entrée 4) — un `Verify` sur un template déjà `Approved`
déclenche l'exception de `Verify()`, pas celle de `Approve()`, mais les deux remontent en 409 par
le même chemin. `Propose` est le seul à reconstruire une structure complexe : il transforme les
DTOs en véritables `TemplateSection`/`TemplateItem`, en générant un `Guid.NewGuid()` par section et
par item — ces identifiants ne viennent jamais du client, ce qui empêche un appelant de forcer un
ID de son choix. Les 4 actions appellent `_currentUser.GetActorAsync` avant même le `try` :
l'acteur réel, relu en base, est donc toujours résolu avant que le use case ne fasse quoi que ce
soit avec, RBAC compris.

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

**Trace d'exécution.** `ExecuteAsync` enchaîne, dans un ordre qui ne doit jamais changer, les
vérifications avant l'action : RBAC (`WorkflowInstantiate`, réservé à HR) → l'employé existe-t-il →
`DepartmentScopeGuard.CanAccessEmployee` (entrée 3, un RH ne peut instancier que pour son propre
pôle) → le DERNIER template `Approved` du bon type (`GetLastApprovedAsync` — la contrainte vient
du circuit de validation qualité, entrées 4 et 19 : un template `Draft` ou `Rejected` ne peut
jamais servir de base). `template.ResolveApplicableItems(employee.ContractType)` (entrée 4) filtre
déjà les items non pertinents avant toute construction — c'est ici, et seulement ici dans le flux
d'onboarding, que la règle « un stagiaire n'a pas de compte SELFRH » (docs/LOGIQUE_METIER.md §5)
prend effet concrètement, chaque item filtré devenant un `ChecklistItemStatus`. Le même
enchaînement — RBAC, portée, résolution de ressource, construction — se retrouve à l'identique
dans les use cases voisins non détaillés ici (`CheckItemUseCase`, `CloseCaseUseCase`,
`ArchiveCaseUseCase`) : une fois ce fichier compris, les autres se lisent par simple reconnaissance
de motif.

---

## Et après ces 20 ?

Si tu veux aller plus loin une fois ces 20 fichiers digérés, dans cet ordre de priorité :
`src/Agirh.Domain/Entities/WorkflowInstance.cs` (le pendant "instance" de l'entrée 4),
`src/Agirh.Infrastructure/Persistence/AgirhDbContext.cs` (mapping EF Core), les fichiers
`tests/Agirh.Tests/Rag/*.cs` (montrent le pipeline RAG testé bout en bout sur le vrai corpus),
et `src/Agirh.Api/Controllers/WorkflowController.cs` (check/close/archive, même style que
l'entrée 19).
