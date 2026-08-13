# AGIRH — Pipeline IA & RAG (V6) — Dossier exhaustif

> **Objectif :** maîtriser **tous les acteurs** du pipeline agentique (rôle, conception, code, dépendances in/out, process, lieu d'intervention, forces/faiblesses, « formation » des modèles), le **RAG** (embedding/vector), le **streaming SSE** et les marqueurs — ancré au code réel (remédiation R1-R8 incluse).
> **⚠️ Aucun `OllamaChatService`/`AIFunctionFactory` : le pipeline actuel est décrit ci-dessous. Les agents sont des modèles Ollama **pré-entraînés** (pas de fine-tuning), pilotés par prompts + réglages + validation C#.**

---

## 1. Résumé exécutif (3 puces)

- **6 acteurs** dans la chaîne : `ProfilerService` (LLM) → `PreFlightValidator` → `ZeroTrustDispatcher`/`RbacMatrix` (RBAC C#, zéro LLM) → `WorkerExecutor` + outils MAF (C#) → `SynthesizerAgent` (LLM) → `CheckerAgent` (LLM, fail-closed) — coordonnés par `AgentOrchestratorService`, servis par `AgentController` (SSE). *(Le `GreetingClassifier` déterministe a été **supprimé** : les salutations passent par le Profiler → `Greeting` → GeneralChat → synthèse → check.)*
- **3 modèles pré-entraînés** : `phi4-mini:3.8b` (Profiler + Synthesizer), `qwen3.5:9b` (Checker, le plus gros), `embeddinggemma` (768d) — budgets `num_predict` bornés, prompts stricts, verdict JSON.
- **UX** : streaming SSE `token` ~30 ms + marqueurs `||WIDGET:SalaryAdvance:{id}||` / `||SUGGEST:…||` ; « Aucun… » et erreurs worker court-circuités **avant** Ollama.

---

## 2. Modèles Ollama & budgets

| Modèle | Rôle | Temp | `num_predict` | `format:"json"` | Endpoint |
|---|---|---|---|---|---|
| `phi4-mini:3.8b` | Profiler (routeur : intention + `query` RAG) | **0** | **512** | ✅ actif (`ProfilerService.cs:68`) | `POST /api/chat` |
| `phi4-mini:3.8b` | Synthesizer (reformulation, 2 phrases max) | **0.3** | **160** | ❌ | `POST /api/chat` |
| `qwen3.5:9b` | Checker (verdict fail-closed) | **0** | **256** | ❌ **retiré** (R1/V6.1, `CheckerAgent.cs:57-59`) + **`think:false` RACINE** (Lot C corrigé) et repli `message.thinking` | `POST /api/chat` |
| `embeddinggemma` | Embeddings RAG | — | — | — | `POST /api/embed` |

- **« Formation »** : aucun fine-tuning. Le comportement de chaque agent = **prompt système** (Règles 1-7 du Profiler, anti-hallucination du Synthesizer, format strict du Checker) + **hyperparamètres** (temperature, `num_predict`) + **garde-fous C#** en aval (PreFlight, RbacMatrix, garde mot-clé R2, bornage 500 mots, Checker fail-closed).
- **`MaxReflectionLoops`** : `1` dans `appsettings.json:31`, mais **défaut code = 2** (`AgentOrchestratorService.cs:55`) — un profil sans la clé rejouerait 2 tours.
- **⚠️ 2 profils de lancement** (`launchSettings.json`) : `Agirh_Bureau` → `Development` (Ollama entreprise `192.168.100.220` ; `ProfilerModel`/`SynthesizerModel`=`phi4-mini:3.8b`, `CheckerModel`=`qwen3.5:9b`) ; `Agirh_Maison` → `local` (Ollama **local** `localhost:11434`, **les 3 rôles chat en `phi4-mini:3.8b`** + `embeddinggemma` 768d, timeouts locaux 600/300/300/300, via `appsettings.local.json`). **Production** : `AI:Endpoint` requis via l'environnement (sinon refus au démarrage).
- **Pas de modèle « worker »** : l'exécution des outils est 100 % C# (déterministe, testable) — le Worker yield des `string` bruts. Le type `ToolResult` (ancien `IWorkerExecutor.cs`) a été **supprimé** (code mort).

---

## 3. Chaîne runtime complète (ancrée)

```
AgentController.ChatAsync                          AgentController.cs:41
 └─ AgentOrchestratorService.ProcessChatRequestAsync   AgentOrchestratorService.cs:62
     ├─ [boucle ≤ 2] ProfilerService (LLM)         :87  (MaxExtractionRetries=2, :54)
     │     └─ garde R2 mot-clé doc → KnowledgeSearch  :132-146
     ├─ RbacMatrix.ResolveTool + PreFlightValidator :127-128
     ├─ ZeroTrustDispatcher.DispatchAsync          :144-145
     │     └─ DispatchRejected → sentinelle \u001fDENIED\u001f :147-160
     ├─ WorkerExecutor (chunks)                    :182-186 → RawWorkerData :187
     ├─ [interception 6 préfixes] « Erreur/Accès refusé/Action non autorisée/Aucun/Aucune/L'opération a été annulée » :171-179 (verbatim, 0 LLM)
     ├─ [bornage R1] BoundWords(500) swap          :216-217 (restore :235)
     ├─ [boucle réflexion ≤ MaxReflectionLoops]    :220-231
     │     ├─ SynthesizerAgent (LLM) → draft       :223
     │     └─ CheckerAgent (LLM, 5 chemins fail-closed) :224
     ├─ dernier draft invalide → NotStreamed (jamais streamé) :239-249
     ├─ [strip \u001f pré-stream]                  :266
     ├─ marqueurs WIDGET (préservé) + SUGGEST      :269-289
     └─ Token Replay (Split + Task.Delay(30ms))    :305-318 → SSE
```

**Court-circuits (dans l'ordre)** : IA indisponible (fallback profiler) → « service temporairement indisponible » ; intention `Unknown` → reformulation ; « Aucun…/Erreur…/Annulé… » du worker → verbatim ; dernier draft invalide → `NotStreamed`. Les salutations suivent le pipeline complet (`Greeting` → GeneralChat → synthèse → checker).

---

## 4. Dossier par composant (conception, code, dépendances in/out, forces/faiblesses)

> Conventions : **IN/OUT** = types exacts ; **Lieu** = où il intervient dans la chaîne ; **Formation** = modèle + prompt + réglages ; **Tests** = fichier(s) qui le verrouillent.

### 4.1 `AgentController` — frontière SSE (aucun LLM)
- **Rôle** : reçoit le message (`[Authorize]`, route `api/agent`, `AgentController.cs:15`), extrait la **Zone Rouge** (identité) du JWT, garantit la possession de la conversation, traduit le flux en frames SSE, persiste + audite dans un `finally`.
- **Lieu** : en amont de tout ; démarre `ProcessChatRequestAsync` (`:121`), **préfixe** le déni en frame `denied` (`:127-135`).
- **Process** : validation message (`error` :47-52) → `HardState` (401 si échec :57-66) → `GetOrCreateConversationAsync` (403 IDOR :73-79) → `RecentMessages.TakeLast(3)` (`:105`, **fournis par le client**) → headers SSE (`text/event-stream`, `no-cache`, `X-Accel-Buffering: no`, `Connection: keep-alive` :110-113) → frame `conversation` (:115) → flux → `denied` unique sans `done` (:127-135) | tokens avec **filtre défensif anti-sentinelle** (:140) → `finally` : persistance + **audit** (troncature PII **400 caractères** Message/Response, R8 :192-193) → `done`.
- **IN** : `ChatRequest {Message, ConversationId?, PreviousMessages?}` (:282-287) + `ClaimsPrincipal`. **OUT** : frames `conversation|token|denied|error|done` (`WriteSseEvent` :256-261) + DB + 1 ligne JSONL d'audit (`ChatAuditEntry` : `Models` Profiler/Synthesizer/Checker/Embedding, `ReflectionLoops`, `CheckerValid`, `WidgetId`, `Suggestion`, `Outcome`).
- **Conception** : DI `IAgentOrchestratorService, IHardStateExtractor, IUnitOfWork, IHttpClientFactory, IChatAuditLogger, IConfiguration` (:25-40). `Health` (`GET api/agent/health` :263-279) interroge **`AI:Endpoint`** configuré (fini le `localhost:11434` codé en dur, Lot B).
- **Forces** : persistance/audit même si le flux explose ; anti-IDOR conversation ; anti-énumération 404 ; filtre défensif.
- **Faiblesses** : `RecentMessages` est une entrée **client non vérifiée** envoyée au Profiler (risque contenu : enum fermé + RBAC + Checker) ; 401/403 court-circuitent l'audit `conversationId`.
- **Tests** : `AgentControllerDeniedTests.cs` (4 : frame denied unique, persistance sans sentinelle, régression token/done, **sentinelle non honorée hors `Outcome.Denied`**).

### 4.2 `AgentOrchestratorService` — le chef d'orchestre (aucun LLM)
- **Rôle** : machine à états déterministe qui **séquence** les 6 acteurs, applique les court-circuits et matérialise l'audit `AgentPipelineOutcome`.
- **Lieu** : point central, **après** le contrôleur et **avant** chaque agent (`AgentOrchestratorService.cs:62`). DI **Scoped** (`Program.cs:204`), injecte les 6 ports + `IOptions<AIOptions>` + logger.
- **Process** : boucle ≤2 extractions (:84-142 : Profiler :87 ; `Unknown`/fallback :90-113 ; `ResolveTool` + PreFlight :127-130 ; clarification à épuisement :132-141) → Dispatch (:144-145 ; `DispatchRejected` → `DenialSentinel + message` :147-160 ; `IntentUnresolvable` :162-170) → Worker (:182-186) → **interception 6 préfixes** (`Erreur`/`Accès refusé`/`Action non autorisée`/`Aucun`/`Aucune`/`L'opération a été annulée` → `WorkerError` verbatim :171-179) → **bornage R1** `BoundWords(fullRawData, 500)` (:216-217, restore `finally` :235) → boucle réflexion `≤ _maxReflectionLoops` (:220-231 : synthèse :223, éval :224) → dernier invalide → `NotStreamed` (:239-249) → **strip `\u001f` pré-stream** (:266) → marqueurs (:269-289) → Token Replay (:305-318).
- **IN** : `AgentPipelineContext` (`ConversationId, RecentMessages, Identity, CognitiveContext, ValidatedParameters, RawWorkerData?, Outcome?`, `IAgentOrchestratorService.cs:6-16`). **OUT** : `IAsyncEnumerable<string>` + `Outcome` rempli.
- **Config** (`IOptions<AIOptions>`) : `MaxExtractionRetries` (2, borné ≥ 1), `MaxReflectionLoops` (1 en config / **défaut 2**), modèles. Sentinelle `internal const DenialSentinel = "\u001fDENIED\u001f"` (:43).
- **Forces** : déterminisme des branches ; fail-closed structurel ; bornage 500 mots ; sentinelle isolée ; strip pré-stream.
- **Faiblesses** : dépend de la qualité du JSON LLM ; latence cumulée (profiler+worker+synthèse+checker) ; écho Checker R1 atténué mais non éliminé.
- **Tests** : `AgentOrchestratorServiceTests.cs` (23 : NotStreamed sans token du draft, SUGGEST, cancellation, flux greeting LLM, reformulation, fallback sans fuite, 6 préfixes verbatim, RoutingError, draft vide, bornage 500 + restauration, WIDGET sans-doublon/GUID strict, token replay) ; `PipelineChainTests.cs` (3, chaîne réelle).

### 4.3 Salutations — supprimées du court-circuit (option c)
- Le **`GreetingClassifier` déterministe a été retiré** (fichiers `GreetingClassifier.cs`, `IGreetingClassifier.cs`, `GreetingClassifierTests.cs` supprimés ; DI retirée de `Program.cs`).
- Les salutations suivent désormais le pipeline **LLM complet** : Profiler (Règle 6 → `Greeting`) → `RbacMatrix.ResolveTool("Greeting")` = `null` → **GeneralChat** → Worker (yield `MainIdea`) → Synthesizer (**règle Greeting** : salutation polie courte, ignore `Données système` :46-47) → **Checker** (**exemption Greeting** : draft poli et bref valide même sans appui données :44-48).
- **Conséquences** : plus de « 0 LLM / < 200 ms » ; `Merci`/`Au revoir` écrasés en `Greeting` (granularité perdue) ; audit `outcome=Success`, `tool=GeneralChat`, `profiler=<modèle réel>` (fini `"skipped"`).
- **Sécurité** : sentinelle `\u001fDENIED\u001f` **gated par `Outcome.Denied`** (contrôleur :135-136) — un écho LLM ne peut plus déclencher de fausse bulle orange ; strip `\u001f` pré-stream (:266).

### 4.4 `ProfilerService` — Agent 1, le routeur (LLM)
- **Rôle** : transforme le dernier tour en **`DynamicContextVector`** : intention (enum fermé, 13 valeurs), confiance, idée principale, **entités extraites** (dont `query` RAG). Sanitize les données sensibles et applique des gardes C# anti-hallucination.
- **Lieu** : **dans la boucle ≤ 2** (`AgentOrchestratorService.cs:87-88`). Si `Unknown` + `ModelUsed=="fallback"` → pipeline `Fallback` ; `Unknown` sinon → reformulation.
- **Process** : `SanitizeContent` → **regex PII** (`password|mot de passe|bank|carte bancaire|iban|rib|nir` + valeur → `***`, `SensitiveDataRegex` :286-287) → format `[ISO] role: content` → `POST /api/chat` (`format="json"` :68, `temp 0`, `num_predict 512` :69, **`think:false` racine**, 2 tentatives :83) → `ExtractJson` (:250-277) → `ParseIntention` (**mapping sur enum fermé → `Unknown` sinon**, :233-248 — mécanisme central anti-injection) → `MapToVector` (:195-231) ; repli RAG `query` = `core_idea` tronqué 20 mots (:211-214) ; **garde R2** (:132-146) : si `GeneralInquiry` + `DocumentKeywordRegex` (`règlement|charte|politique|procédure|processus|guide` :289) → **force `KnowledgeSearch`**.
- **IN** : `CognitiveExtractionInput {RecentMessages, ConversationId}`. **OUT** : `DynamicContextVector {Intention, ConfidenceScore, MainIdea, Urgency, Tone, IsFollowUp, TriggerPhrase, ExtractedEntities (employee_name, employee_id, leave_request_id, category, amount, date_reference, days, query), ProcessingTimeMs, ModelUsed, InputTokens, OutputTokens}`.
- **Formation** : modèle pré-entraîné **`phi4-mini:3.8b`** (`AI:ProfilerModel`), prompt système `ProfilerService.cs:292-339` : routeur, JSON uniquement, **Règles 1-7** (R1 STC→`PayrollSettlement` ; R2 solde→`LeaveBalance` ; R3 « solde » seul→`PayrollSettlement` ; R4 avance→`SalaryAdvance` ; R5 `query` obligatoire pour `KnowledgeSearch` ; R6 salutation→`Greeting` ; **R7 document→`KnowledgeSearch`**), 9 exemples few-shot. `temp 0`, `num_predict 512`, `format:"json"`, `think:false` racine.
- **Conception** : DI **Transient** `AddHttpClient<ICognitiveProfiler, ProfilerService>` (`Program.cs:175-179`), timeout `AI:ProfilerTimeout` (300 s, 600 s dev), Polly retry 3× (200/400/800).
- **Forces** : `temp 0` ; garde R2 ; sanitisation PII **avant** LLM ; repli déterministe `query` ; 2 tentatives + fallback propre (`Unknown`/`fallback`).
- **Faiblesses** : dépend de la qualité du JSON ; latence (≤2 × 300 s) ; `InputTokens/OutputTokens` = 0 (audit inexact) ; regex PII finie (PII hors mots-clés possible).
- **Tests** : `ProfilerServiceTests.cs` (8 : repli query, troncature 20 mots, mapping Greeting, Règle 6/7 dans le prompt, **garde R2 force KnowledgeSearch**).

### 4.5 `PreFlightValidator` — validation C# (aucun LLM)
- **Rôle** : contrôle de **présence** des paramètres extraits **avant** tout dispatch ; un outil ne reçoit que des paramètres validés.
- **Lieu** : dans la boucle d'extraction, immédiatement après le Profiler et **avant** le Dispatcher (`AgentOrchestratorService.cs:128-141`). **Budget : 2 tentatives max** (`AI:MaxExtractionRetries=2`, :54 — **pas 3**).
- **Process** : `ValidateParameters(toolName, extractedEntities)` (`PreFlightValidator.cs:8-79`) — `employee_id` optionnel (Consulter/Avance/Poser, repli JWT) ; `leave_request_id` **requis** (Approuver) ; `employee_id` requis (Révoquer) ; `query` **requis** (RAG) ; `amount` **strictement requis** (Avance) ; `category` optionnelle mais **whitelist** `Administratif|IT|RH|Management` (:52-57) ; `date_reference` + `days` **requis** (Poser, R5 :59-72). Échec → `ValidationResult(false, "Paramètres obligatoires manquants : …")`.
- **IN** : `(toolName, extractedEntities)`. **OUT** : `ValidationResult(IsValid, ErrorMessage?, ValidatedJson)` — **`ValidatedJson` prime sur les entités extraites** au dispatch (P0-1, `ZeroTrustDispatcher.cs:188-203`).
- **Forces** : déterministe, messages de clarification exploitables ; **limite de portée à connaître** : présence + whitelist seulement — les validations **sémantiques** vivent dans les outils (date/jours `LeaveFunctions.cs:49-53,72-78`, montant/plafond `CreateSalaryAdvanceRequest.cs:56-62`, conversion string→decimal `IMafTool.cs:30-39`).
- **Faiblesses** : liste d'outils **maintenue à la main** (un nouvel outil MAF doit y être ajouté).
- **Tests** : `PreFlightValidatorTests.cs` (6 : catégorie inconnue/valide/absente, pose sans date/sans jours/complet) ; `PipelineChainTests.cs` (montant manquant → 2 appels profiler, 0 ligne en base).

### 4.6 `ZeroTrustDispatcher` + `RbacMatrix` — Agent 2, le porte de sécurité (aucun LLM)
- **Rôle** : décide si l'intention peut être routée vers un outil MAF : confiance → état du compte → **RBAC rôle** → **scope manager** → dispatch. Résultat **discriminé** (`DispatchResult`).
- **Lieu** : après PreFlight, **avant** le Worker (`AgentOrchestratorService.cs:144-145`). DI **Scoped** (`Program.cs:182`).
- **Process — ordre des gates** (`ZeroTrustDispatcher.cs:28-120`) : (1) confiance < 0.4 → `IntentUnresolvable` « Pouvez-vous préciser… » (:33-40) ; (2) **`IsActive` d'abord** → `ACCOUNT_INACTIVE` (:42-57, fail-closed avant tout RBAC) ; (3) `FindTool` = `RbacMatrix.ResolveTool` — **intention non mappée (`GeneralInquiry`/`SmallTalk`) → `GeneralChat`** (:65-76, **sans entrée matrice**) ; (4) rôle `(identity.RoleFlag & requiredRoles) == 0` → `INSUFFICIENT_ROLE` (:78-98) ; (5) **scope uniquement si `RoleFlag == Manager`** (`EvaluateScopeAsync` :131-163, `EvaluateOwnerScope` :165-175 : self OK / subordonné direct OK / sinon `SCOPE_MISMATCH`) ; approbation/historique via `leave_request_id` (`RequiresManagerScope`) ; **Admin exempté** ; (6) `DispatchToTool` (:109-119).
- **Matrice** : `RbacMatrix.Default` (`RbacMatrix.cs:18-42`) — **10 outils** (rôles + `RequiresManagerScope`) ; mapping **9 intentions** → outils (enum fermé, jamais de string LLM libre) : `LeaveRequest → PoserDemandeCongesAsync` (:34, R5), `SalaryAdvance → DemanderAvanceSalaireAsync` (:41), `KnowledgeSearch → RechercherInformationRagAsync`… ; `ResolveTool` :44-48, `ParseAllowedRoles` :56-70 (rôle inconnu → `RoleFlags.None` = refus).
- **IN** : `DispatchInput {CognitiveContext, Identity (HardState), ValidatedParameters?}`. **OUT** : `DispatchToTool(ToolDispatch)` | `DispatchRejected(AccessDenied : DenialCode INSUFFICIENT_ROLE/SCOPE_MISMATCH/ACCOUNT_INACTIVE)` | `IntentUnresolvable(string)`.
- **⚠️ Message `denied`** : la **décision** vient du dispatcher, mais le **texte** de la bulle est **généré par l'orchestrateur** (template intention + rôle, `AgentOrchestratorService.cs:158`) — les messages spécialisés `FormatDenialMessage` (`ZeroTrustDispatcher.cs:224-235`) ne sont **jamais affichés** (loggés uniquement).
- **⚠️ IDOR Collaborator** : le scope dispatcher ne s'applique **qu'au Manager** → la protection Collaborator est déléguée aux **gardes internes des outils MAF** (self-only : `ConsulterSoldeTool` :31-32, `ConsulterHistoriqueCongesTool` :110-111, `PoserDemandeCongesTool` :46-47) et au use case avance (`CreateSalaryAdvanceRequest.cs:45-47`).
- **Forces** : déterminisme RBAC absolu ; fail-closed (inactif → confiance → rôle → scope) ; trou scope orphelin corrigé ; paramètres validés prioritaires (P0-1).
- **Faiblesses** : dépend de la qualité des entités extraites (mauvaise extraction → déni fail-safe) ; outils hors matrice non protégés ; `GeneralChat` contourne la matrice (sans données sensibles, défendable).
- **Tests** : `ZeroTrustDispatcherTests.cs` (11) ; `RbacMatrixTests.cs` (3 : 9 intentions, LeaveRequest ≠ historique, non mappées → null) ; `PipelineChainTests.cs`.

### 4.7 `WorkerExecutor` + outils MAF — Agent 3, l'exécuteur (aucun LLM)
- **Rôle** : résout l'outil MAF désigné et convertit son résultat en chunks `IAsyncEnumerable<string>` accumulés en `RawWorkerData`. **Outils = C# métier** (lecture/écriture DB), jamais de LLM.
- **Lieu** : après un `DispatchToTool` réussi, **avant** le Synthesizer (`AgentOrchestratorService.cs:181-187`). **Contournement** : `GeneralChat` → `yield MainIdea` (`WorkerExecutor.cs:27-38`).
- **Process** : outil introuvable → « Erreur système : l'outil 'X' n'est pas disponible. » (:40-47) → `JsonSerializer` → `tool.ExecuteAsync(parameters, input.HardState.UserId, ct)` (:56, **UserId = JWT, jamais fourni par l'IA**) → exception → **message générique** (jamais `ex.Message`, :64-69) → détection erreur (`Erreur`/`Accès refusé`/`Aucun` → Warning :79-84) → yield (:86).
- **Outils MAF** (10, **Scoped**, découverte réflexive `Program.cs:141-148`, OCP : ajouter un outil = 1 classe `IMafTool`) :
  | Outil | Rôles | Effet |
  |---|---|---|
  | `ConsulterSoldeAsync` (`EmployeeFunctions.cs:10`) | All | solde + CET ; IDOR self-only |
  | `GenererSoldeToutCompteAsync` (:47) | Admin\|Manager | STC + indemnités |
  | `RevoquerAccesITAsync` (`ITSecurityFunctions.cs:10`) | Admin | désactive `IsActive` |
  | `EnvoyerAlerteManagerAsync` (:42) | Admin\|Manager | alerte au manager |
  | `PoserDemandeCongesAsync` (`LeaveFunctions.cs:19`, **R5**) | All | crée `LeaveRequest` **Pending** + `SaveChangesAsync` ; IDOR self |
  | `ConsulterHistoriqueCongesAsync` (:83) | All | historique trié |
  | `ApprouverDemandeCongesAsync` (:132) | Admin\|Manager | scope subordonnés ; Approved/Rejected si Pending |
  | `GenererChecklistAsync` (`ChecklistFunctions.cs:10`) | Admin\|Manager | checklist par catégorie/ordre |
  | `RechercherInformationRagAsync` (`RagFunctions.cs:11`) | All | embedding → top-5 `VECTOR_DISTANCE` + « **Source : …** » |
  | `DemanderAvanceSalaireAsync` (`PayrollFunctions.cs:12`) | All | use case `CreateSalaryAdvanceRequest` |
- **Use case avance** (`Core/Services/CreateSalaryAdvanceRequest.cs`) : identité fail-closed (:33-34), anti-IDOR (:46-47), profil paie requis (:49-51), anti-doublon Pending (:53-54, + index unique filtré `AppDbContext.cs:122-124`), plafond `NetSalary × MaxAdvancePercentage` (:59-62), persistance Pending (:67-76), **émet `||WIDGET:SalaryAdvance:{id}||`** (:78).
- **IN** : `ExecutionInput {Dispatch, HardState, CognitiveContext}`. **OUT** : chunks `string`.
- **Forces** : déterministe, testable ; masquage d'erreurs ; conversion string→decimal à la frontière (P0-2) ; requêtes paramétrées (aucune injection SQL, `KnowledgeDocumentRepository.cs:36-46`).
- **Faiblesses** : renvoie du **texte brut** (le Synthesizer reformule — risque d'écho R1 atténué par le bornage 500 mots) ; `GeneralChat` « répond » avec `MainIdea` du profiler (pas de vraie génération).
- **Tests** : `LeaveFunctionsTests.cs` (4), `ChecklistFunctionsTests.cs` (3), `SalaryAdvanceIntegrationTests.cs` (16), `PipelineChainTests.cs` (décimale 2000m + Pending + WIDGET).

### 4.8 `SynthesizerAgent` — Agent 4, l'acteur (LLM)
- **Rôle** : reformule le `RawWorkerData` en réponse professionnelle **de 2 phrases maximum**, strictement ancrée aux données. Intègre le `feedback` du Checker (Acteur-Critique).
- **Lieu** : après le Worker et l'interception des erreurs, **dans la boucle de réflexion** (`AgentOrchestratorService.cs:223`) ; reçoit un contexte **borné à 500 mots** (R1).
- **Process** : message `"Intention détectée : {intention}\nDonnées extraites du système :\n{rawData}"` (+ feedback) (:49-51) → `POST /api/chat` (`temp 0.3`, `num_predict 160`, **`think:false` racine** :57) → échec → **retourne le `rawData` brut** (jamais de texte inventé, le Checker jugera, :78-82).
- **IN** : `(AgentPipelineContext, string? feedback, CancellationToken)`. **OUT** : `Task<string>` (jamais d'exception).
- **Formation** : modèle pré-entraîné **`phi4-mini:3.8b`** (`AI:SynthesizerModel` ?? `AI:ProfilerModel`), prompt `SynthesizerAgent.cs:39-47` : « 2 phrases MAXIMUM », « Ne spécule PAS », « Si les données ne contiennent PAS l'information → réponds exactement : *Je n'ai pas trouvé cette information dans la base de connaissances.* — n'invente JAMAIS » (R3), + règle Greeting. `temp 0.3` (légère variabilité), `num_predict 160`, `think:false` racine.
- **Conception** : DI **Transient** `AddHttpClient<ISynthesizerAgent, SynthesizerAgent>` (`Program.cs:191-195`), timeout `AI:SynthesizerTimeoutSeconds` (120 s), Polly retry 3×.
- **Forces** : anti-hallucination explicite ; repli rawData ; budget borné ; modèle petit et rapide.
- **Faiblesses** : latence LLM en série ; petit modèle → reformulation imparfaite des RAG longs ; `temp 0.3` réintroduit du non-déterminisme (compensé par le Checker) ; repli rawData peut exposer des données non reformulées.
- **Tests** : **aucun fichier dédié** — couvert via `PipelineFakes.FakeSynthesizer` + `AgentOrchestratorServiceTests.cs`. *(R3 n'est pas verrouillé par un test automatisé.)*

### 4.9 `CheckerAgent` — Agent 5, le critique fail-closed (LLM)
- **Rôle** : valide le draft contre (a) l'intention, (b) les données système, (c) la pertinence, (d) le ton → verdict JSON strict `{"is_valid": bool, "actionable_feedback": "…"}`. **Fail-closed : tout chemin non nominal → `false`**.
- **Lieu** : dans la boucle de réflexion, **immédiatement après** le Synthesizer (`AgentOrchestratorService.cs:224`), **avant les marqueurs et le streaming**. Si invalide → `feedback` pour un 2e tour (si budget).
- **Process** : message `"Intention : {intention}\nDonnées système : {rawData}\nProjet de réponse à évaluer : {draftResponse}"` → `POST /api/chat` (**`format:"json"` retiré** V6.1 : `CheckerAgent.cs:57-59`, `temp 0`, `num_predict 256`, **`think:false` RACINE**) → **5 chemins fail-closed** (`EvaluationResult(false)`) : HTTP non-2xx (:77-83) · contenu vide/nul (:92-96) · JSON introuvable (`ExtractJsonObject` :121-133) (:98-103) · désérialisation null (:105-110) · **exception réseau/timeout** (:114-118) → nominal : `EvaluationResult(IsValid, ActionableFeedback)` (:112). *`think` dans `options` est IGNORÉ par Ollama → `think:false` est un champ racine du body.*
- **IN** : `(AgentPipelineContext, string draftResponse, ct)` (contexte **borné 500 mots**). **OUT** : `EvaluationResult(IsValid, ActionableFeedback)`.
- **Formation** : modèle pré-entraîné **`qwen3.5:9b`** (Bureau, le plus gros des 3, choisi pour la fiabilité du jugement ; local Maison = `phi4-mini:3.8b`). Prompt strict `CheckerAgent.cs:39-51` : cohérence avec l'intention, **`is_valid:false` si une affirmation n'est pas appuyée par les données**, « FORMAT STRICT : zéro texte avant/après le JSON », **« Interdiction de recopier le draft ou les données système »** (anti-écho R1), **exemption Greeting**. `temp 0`, `num_predict 256` (verdict ~15-25 tokens + marge), `think:false` racine.
- **Conception** : DI **Transient** `AddHttpClient<ICheckerAgent, CheckerAgent>` (`Program.cs:197-201`), timeout `AI:CheckerTimeoutSeconds` (120 s), Polly retry 3×. Log du corps modèle tronqué 300 caractères (:88) — peut contenir du texte RAG.
- **Forces** : fail-closed systématique (5 chemins) ; modèle le plus grand ; budget 64 anti-écho ; draft invalide **jamais streamé**.
- **Faiblesses** : **écho historique R1** (atténué : bornage 500 mots + `num_predict 256` + prompt + suppression `format:"json"` + test dédié) ; latence supplémentaire ; **ne vérifie pas la vérité des données** (s'appuie sur les données fournies — ne détecte pas une falsification des données elles-mêmes).
- **Tests** : `CheckerAgentTests.cs` (13 : contenu vide/nul, non-JSON, JSON malformé, `HttpRequestException`, verdict valide/invalide + feedback, modèle configuré, **écho doc long → rejet (R1)**, **`num_predict:256` + `think:false` racine + prompt anti-écho (R1)**).

### 4.10 Marqueurs & Token Replay (post-checker, aucun LLM)
- **Rôle** : livraison UX déterministe : les marqueurs sont ajoutés **après** validation (le LLM n'évalue que le contenu humain) ; le Token Replay simule l'inférence côté client (~30 ms).
- **Lieu** : section de livraison de l'orchestrateur (`AgentOrchestratorService.cs:251-318`), **pas** sur les chemins court-circuités.
- **Process** : draft vide → « Erreur lors de la génération de la réponse finale. » (:254-258) → **WIDGET** : regex `\|\|WIDGET:SalaryAdvance:[0-9a-fA-F-]{36}\|\|` extraite de `RawWorkerData` **complet** ; si perdu par le synthétiseur → **ré-apposé** (jamais dupliqué, :269-276) → **SUGGEST** par intention (:279-289 : `LeaveBalance → "||SUGGEST:Poser un congé||"`, `SalaryAdvance → "||SUGGEST:Suivre ma demande||"`) → `Outcome` (:293-301) → **Token Replay** `Split(' ')` + `Task.Delay(30, ct)` + `ct.ThrowIfCancellationRequested()` (:305-318).
- **⚠️ Contrat frontend strict** : `markers.ts` (GUID 8-4-4-4-12, `stripPartialMarkers` anti-flash, `stripControlSentinel`) — la regex serveur est **plus permissive** (GUID non canonique retiré silencieusement par l'UI).
- **Forces** : le LLM n'évalue jamais les marqueurs ; WIDGET préservé ; streaming fluide ; audit widget/suggestion.
- **Faiblesses** : Token Replay = **simulation** (le texte complet existe déjà en mémoire) ; `Split(' ')` éclate les retours à la ligne ; mots très longs → stream saccadé.
- **Tests** : `AgentOrchestratorServiceTests.cs:162-177` (SUGGEST), `PipelineChainTests.cs:205-208` (WIDGET+SUGGEST), `AgentControllerDeniedTests.cs`.

---

## 5. Frontière LLM & données (qui voit quoi)

- **Seul le Profiler reçoit les messages utilisateur** (après `SanitizeContent` : regex PII `password/banque/IBAN/NIR… → ***`, `ProfilerService.cs:39-42,286`). Le Synthesizer et le Checker ne reçoivent que `intention` + `rawData` (borné 500 mots) + draft.
- **Sorties LLM mappées sur enum fermé** : `ParseIntention` (`ProfilerService.cs:233-248`) accepte uniquement les 12+1 valeurs connues → sinon `Unknown`. C'est le mécanisme central qui a bloqué **3/3 injections** (cf. `AGIRH_MODEL_AUDIT.md` §2).
- **L'historique conversationnel** (`RecentMessages`, `TakeLast(3)`) est **fourni par le client** (`AgentController.cs:88-105`) — entrée non vérifiée, risque contenu par l'enum fermé + RBAC + Checker.
- **Aucune description d'outil n'est envoyée au LLM** — les tool-calls sont routés par mapping C# (`RbacMatrix`), jamais choisis par le modèle.
- **R8 / hygiène** : audit JSONL Message/Response tronqués à **400 caractères** (`AgentController.cs:192-193`) ; log Checker du corps modèle tronqué 300.

---

## 6. RAG (Retrieval-Augmented Generation)

| Étape | Détail | Ancrage |
|---|---|---|
| **Chunking** | fenêtre 512 mots / overlap 128 | `IngestionService.ChunkText` (`IngestionService.cs:15-16,155-173`) |
| **Batching** | lots de 10 + délai 200 ms | `IngestionService.cs:44-58` |
| **Stockage** | `Embedding` byte[] ↔ JSON via **ValueConverter** | `AppDbContext.cs` (`SerializeEmbedding`) |
| **Colonne** | `vector(768)` (SQL Server 2025) — migration `DropColumn+AddColumn` | `20260731134829` |
| **Recherche** | `VECTOR_DISTANCE('cosine', Embedding, CAST(… AS vector(768)))` (paramètre bindé) ; repli `CreatedAt DESC` | `KnowledgeDocumentRepository.cs:36-73` |
| **Top-K** | 5 documents (`SearchBySimilarityAsync(…, 5)`) | `RagFunctions.cs:44` |
| **Query RAG** | produite par le Profiler (`query`), repli `core_idea`/20 mots | `ProfilerService.cs:211-214` |
| **Garde dimension** | `Embedding:ExpectedDimension` (768) lu + fail-fast | `OllamaEmbeddingGenerator.cs:21,57-60` |

> **Notion** : RAG = ancrer la réponse sur la base de connaissances RH au lieu de répondre « de mémoire ». Échec d'embedding/LLM → réponse dégradée mais **jamais de fuite** de données techniques. `FROM` SQL paramétré → aucune injection SQL.

---

## 7. Streaming SSE & marqueurs

### Protocole (`AgentController.cs`)
| Événement | Format | Cas |
|---|---|---|
| `conversation` | `data: {"type":"conversation","data":"<guid>"}` | création/attachement |
| `token` | `data: {"type":"token","data":"mot "}` | Token Replay ~30 ms |
| `denied` | `data: {"type":"denied","data":"<message poli>"}` | déni RBAC (1 seule frame, **pas de `done`**, :127-135) |
| `error` | `data: {"type":"error","data":"…"}` | message requis / 401 / 403 |
| `done` | `data: {"type":"done","data":"[DONE]"}` | fin normale |

### Marqueurs (post-checker, déterministes)
| Marqueur | Déclencheur | Frontend |
|---|---|---|
| `\|\|WIDGET:SalaryAdvance:{guid}\|\|` | avance créée (`CreateSalaryAdvanceRequest.cs:78`), préservé par l'orchestrateur | carte `SalaryAdvanceCard` (marqueur masqué) |
| `\|\|SUGGEST:Poser un congé\|\|` | intention `LeaveBalance` | chip cliquable |
| `\|\|SUGGEST:Suivre ma demande\|\|` | intention `SalaryAdvance` | chip cliquable |

> Frontend : `lib/chat/markers.ts` — GUID strict 8-4-4-4-12, `stripPartialMarkers` (anti-flash), `stripControlSentinel` (défense `\u001f`).

---

## 8. Résilience & timeouts (Polly)

| Client | Timeout | Retry |
|---|---|---|
| Profiler | 300 s (600 s dev, 30 s prod) | `WaitAndRetryAsync(3)` 200/400/800 |
| Synthesizer | 120 s | idem |
| Checker | 120 s | idem |
| Embedding | **180 s** | + `Or<TaskCanceledException>(inner is TimeoutException)` |
| Health | 15 s | — |

> **Notion** : **backoff exponentiel** (200→400→800 ms) pour les pannes transitoires Ollama ; la **cancellation utilisateur n'est jamais rejouée** (on ne retente que les vrais timeouts).

---

## 9. Tests verrouillés & angles morts

**Verrouillé par la suite (122/122)** : fail-closed Checker (13 cas dont écho R1 et budget), RBAC/scope/IDOR (11 dispatcher + 3 matrice + 3 chaîne), R4 « Aucune… » verbatim, R5 congés (4), checklist (3), PreFlight (6), bornes financières 5000/5000.01, contrat SSE denied frame-par-frame, flux greeting LLM, bornage 500 + restauration, token replay.

**Angles morts (à connaître — pas « verrouillés »)** :
- **R1 bornage 500 mots** : le swap/restore n'est **pas asseré** par un test.
- **R3** : `SynthesizerAgent` n'a **aucun test** (ni prompt anti-hallucination, ni `num_predict:160`).
- **R8** : troncature 400 de l'audit **non testée**.
- **Injections prompt** : les « 3/3 injections → Unknown » viennent d'une **session de logs réelle** (audit #3/#7/#12), pas d'un test automatisé.
- **`MaxReflectionLoops`** : les tests configurent `2` et vérifient `CallCount==2` — la **valeur 1** de prod n'est pas testée en soi.
- **E2E Ollama réel** : aucun test contre un vrai serveur (tous les appels passent par `StubHttpMessageHandler`).
- **HTTP middleware** (FallbackPolicy 401, rate-limit 429, CORS) : pas de `WebApplicationFactory`/TestServer.

---

## 10. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0 | `00` + `07` | ✅ |
| 1 | `01_CORE_ARCHITECTURE` | ✅ |
| 2 | `02_SECURITY_AND_RBAC` | ✅ |
| 3 | `03_AI_AND_RAG_PIPELINE` (dossier exhaustif R1-R8) | ✅ |
| 4 | `04_QA_AND_TESTING` + `05` | ✅ |
| 5 | `06_Docker_Orchestration` | ✅ |
