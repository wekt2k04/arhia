# AGIRH — Architecture & Patterns (V6)

> **Objectif :** comprendre l'architecture hexagonale, les entités, la DI, les migrations, et les **patterns/choix de robustesse** — ancré au code réel.
> **État :** build backend 0/0 · tests **122/122** · frontend Next.js 15.5.22 (build 0 erreur).

---

## 1. Résumé exécutif (3 puces)

- **4 projets .NET + 1 frontend** : `Domain` (pur) ← `Core` (ports + use cases) ← `Infrastructure` (adaptateurs) ← `Api` (composition root) ; `frontend/` (Next.js, découplé par BFF).
- **Patterns clés** : Hexagonal (Ports & Adapters), **OCP** (découverte dynamique MAF), **DIP**, **fail-closed**, UoW, timeouts bornés + **Polly**.
- **Base** : SQL Server 2025 (`vector(768)` natif pour le RAG), 5 migrations, index unique filtré anti-TOCTOU.

---

## 2. Couches & dépendances

| Couche | Contenu | Dépend de | Ne référence JAMAIS |
|---|---|---|---|
| `Agirh.Domain` | Entités + ports repo (`IEmployeeRepository`…) | rien (0 dép) | — |
| `Agirh.Core` | `RbacMatrix`, use cases (`CreateSalaryAdvanceRequest`), ports du pipeline (`ICognitiveProfiler`, `IPreFlightValidator`, `IZeroTrustDispatcher`, `IWorkerExecutor`, `ISynthesizerAgent`, `ICheckerAgent`, `IHardStateExtractor`, `IAgentOrchestratorService`, `IJwtTokenService`), `Settings/AIOptions` | Domain | Api, Infrastructure |
| `Agirh.Infrastructure` | `AppDbContext`, repos, `UnitOfWork`, agents IA, MAF tools, `JwtTokenService`, `OllamaEmbeddingGenerator` | Domain, Core | Api |
| `Agirh.Api` | Contrôleurs, `Dtos/`, `Program.cs` (composition root) | Domain, Core, Infra | — |
| `frontend/` | Next.js App Router, BFF, store, composants | Api (HTTP via BFF) | Domain, Core, Infra |

**Chaîne runtime chat :** `AgentController(SSE) → AgentOrchestratorService` → `ProfilerService` (LLM, boucle ≤ 2, salutations → `Greeting`) → `PreFlightValidator` → `ZeroTrustDispatcher(RbacMatrix)` → `WorkerExecutor` → MAF tools (C#) → **[interception « Aucun…/Erreur…/Annulé… »]** → **`SynthesizerAgent` (LLM)** → **`CheckerAgent` (LLM, fail-closed 5 chemins)** — boucle réflexion ≤ `MaxReflectionLoops` → marqueurs → Token Replay → SSE. (Détail exhaustif : `03_AI_AND_RAG_PIPELINE.md` §3-4.)

---

## 3. Entités (résumé)

| Entité | Champs clés | Règle |
|---|---|---|
| `Employee` | Id, Email (unique, lower), Role, ManagerId, LeaveBalance, CET, **IsActive** (défaut `true`) | garde rôle au setter ; **fail-closed à l'extraction** : claim absent → `false` (`HardStateExtractor.cs:30`) |
| `LeaveRequest` | EmployeeId, Type, Status, DaysRequested, ApprovedById | `CanApprove = Pending` |
| `PayrollProfile` | EmployeeId, NetSalary, Iban, MaxAdvancePercentage | cap avance 50 % |
| `SalaryAdvanceRequest` | AmountRequested, Status, RequestDate | **1 Pending/employé** |
| `KnowledgeDocument` | ChunkText, **Embedding** (byte[]↔`vector(768)`), SourceFile | ValueConverter JSON |
| `ChecklistItem` | Title, Category, IsRequired, Order | catégories RH/IT/… |
| `AgentConversation`/`AgentMessage` | UserId, Messages, Role, Content, Timestamp | historique chat |

> Détails complets : `docs/notebooklm/07_BUSINESS_DOMAIN.md`.

---

## 4. Patterns & notions (ancrés au code)

| Pattern | Pourquoi | Où |
|---|---|---|
| **Hexagonal (Ports & Adapters)** | Domain pur, Infrastructure swappable, contrats stables | `Domain/Interfaces/*`, `Core/Interfaces/*` |
| **OCP — découverte dynamique** | Ajouter un outil = 1 classe `IMafTool` ; **zéro** modif router | `Program.cs:141-148` (scan assembly) |
| **DIP** | Les contrôleurs dépendent de ports, pas de classes concrètes | `IJwtTokenService` (Core), `OllamaEmbeddingGenerator` (Infra) |
| **UoW + Repositories** | Persistance atomique ; lecture `AsNoTracking` | `UnitOfWork.cs`, `Repositories/*` |
| **Use case pur** | Logique métier avance dans Core, pas dans le contrôleur | `Core/Services/CreateSalaryAdvanceRequest.cs` |
| **Fail-closed** | Toute erreur/absence → refus (jamais accès par défaut) | `CheckerAgent` (**5 chemins** → `EvaluationResult(false)`, `CheckerAgent.cs:77-118`), `HardStateExtractor` (claim absent → `false`), `FallbackPolicy` |

---

## 5. Composition root — DI (`Program.cs`)

| Service | Lifetime | Note |
|---|---|---|
| `AppDbContext` / repos / `UnitOfWork` | Scoped | SQL Server + retry EF (5×, 30 s) |
| `IMafTool` (découverte réflexion) | Scoped | `Program.cs:141-148` |
| `IHardStateExtractor` / `IAgentOrchestratorService` / `IPreFlightValidator` / `IZeroTrustDispatcher` / `IWorkerExecutor` | Scoped | logique C# du pipeline (`Program.cs:165,182,185,188,204`) |
| `ICognitiveProfiler` / `ISynthesizerAgent` / `ICheckerAgent` | Transient (`AddHttpClient`) | timeouts bornés + Polly |
| `IEmbeddingGenerator` | Transient (`AddHttpClient`) | timeout **180 s** + retry TimeoutException |
| `IngestionService` | Scoped | RAG batching |
| `IHttpContextAccessor` | Singleton | claims → MAF |
| Logging & audit | `FileLoggerProvider` + `ChatAuditLogger` (singletons) | `agirh-api.log` + `agirh-audit.jsonl` — **réinitialisés à chaque démarrage** ; `NoopChatAuditLogger` en secours (`Program.cs:24-51`) |
| Auth / Authorization | — | **FallbackPolicy = RequireAuthenticatedUser()** |
| Rate limiter | — | login 5/min, chat 20/min, 429 |
| CORS | — | `ProdWhitelist` (prod) / `AllowAll` (dev) |

> **Timeouts (aucun `TimeSpan.Zero`)** : health 15 s · profiler 300 s (600 s en Development) · synthèse 120 s · checker 120 s · **embedding 180 s**. Retry Polly `WaitAndRetryAsync(3)` backoff 200/400/800 ms.

## 6. Le pipeline IA — dossiers par composant (conception)

> Dossiers complets (process, prompts, forces/faiblesses, tests) : `03_AI_AND_RAG_PIPELINE.md` §4. Ici : la synthèse conception/dépendances in-out/lieu.

| # | Composant | LLM | Lieu d'intervention | Conception (code, DI) | IN → OUT |
|---|---|---|---|---|---|
| 0 | `AgentController` | non | frontière HTTP/SSE, amont de tout | `AgentController.cs:41`, DI 6 ports | `ChatRequest` + JWT → frames `conversation/token/denied/error/done` + DB + audit JSONL (troncature 400, **PII masquée**) |
| 1 | `AgentOrchestratorService` | non | chef d'orchestre : séquence + court-circuits | Scoped (`Program.cs:204`), injecte les 6 ports, sentinelle `\u001fDENIED\u001f` | `AgentPipelineContext` → `IAsyncEnumerable<string>` + `Outcome` |
| 2 | `ProfilerService` | **phi4-mini:3.8b** | routeur (salutations → `Greeting`), boucle ≤ 2 (retry PreFlight) | Transient `AddHttpClient` (`Program.cs:175`), `temp 0`, `num_predict 512`, `format:"json"`, `think:false` racine, Règles 1-7 | `CognitiveExtractionInput` → `DynamicContextVector` (intention enum fermé, entités, `query`) |
| 3 | `PreFlightValidator` | non | **entre** Profiler et Dispatcher, 2 passes max | Scoped (`Program.cs:188`), whitelists par outil | `(toolName, entités)` → `ValidationResult` (ValidatedJson **prioritaire**) |
| 4 | `ZeroTrustDispatcher` + `RbacMatrix` | non | **avant** le Worker ; 1 seule matrice `RbacMatrix.Default` | Scoped (`Program.cs:182`) ; gates : confiance → IsActive → rôle → scope Manager → dispatch | `DispatchInput` → `DispatchToTool` / `DispatchRejected(DenialCode)` / `IntentUnresolvable` |
| 5 | `WorkerExecutor` + MAF tools | non (RAG via `embeddinggemma`) | exécution déterministe, **avant** la synthèse | Scoped (`Program.cs:185`) ; 10 outils MAF découverts par réflexion (`Program.cs:141-148`) | `ExecutionInput` → chunks `string` → `RawWorkerData` |
| 6 | `SynthesizerAgent` | **phi4-mini:3.8b** | acteur de la boucle réflexion (bornage 500 mots, règle Greeting) | Transient `AddHttpClient` (`Program.cs:191`), `temp 0.3`, `num_predict 160`, `think:false` racine | `(context, feedback)` → `string` (draft 2 phrases max) |
| 7 | `CheckerAgent` | **qwen3.5:9b** | critique de la boucle, **avant** marqueurs/streaming | Transient `AddHttpClient` (`Program.cs:197`), `temp 0`, `num_predict 256`, `format:"json"` retiré, **`think:false` RACINE** + repli `message.thinking`, exemption Greeting | `(context, draft)` → `EvaluationResult(IsValid, ActionableFeedback)` — **5 chemins → false** |
| 8 | Marqueurs + Token Replay | non | post-validation, livraison | `AgentOrchestratorService.cs:251-318` ; regex WIDGET + SUGGEST par intention ; **strip `\u001f` pré-stream** | `finalDraft` → jetons ~30 ms ; `WidgetId`/`Suggestion` audités |

**Régime de dépendances (in/out clés)** :
- Les agents LLM sont **Transient** (`AddHttpClient` + Polly retry 3× backoff 200/400/800) ; la logique C# est **Scoped**.
- Le `HardState` (identité) est extrait **exclusivement du JWT** (`HardStateExtractor.cs:10-31`, `is_active` absent → `false` :30) — jamais influençable par le message.
- **Frontière LLM** : seul le Profiler reçoit les messages utilisateur (sanitisés PII) ; Synthesizer/Checker ne reçoivent que `intention` + `rawData` (≤ 500 mots) + draft ; les sorties LLM sont mappées sur un **enum fermé** (`ParseIntention` → `Unknown`).
- `MaxExtractionRetries` = 2 (config, `AgentOrchestratorService.cs:54`) ; `MaxReflectionLoops` = 1 en config / **défaut code 2** (:55).

---

## 7. Robustesse & résilience

| Risque | Garde-fou | Preuve |
|---|---|---|
| SQL redémarre | `EnableRetryOnFailure(5, 30 s)` + `CommandTimeout(60)` | `Program.cs:59-67` |
| Ollama lent/down | timeouts bornés + retry Polly ; `AI_UNAVAILABLE` (fallback profiler) → message générique, jamais de fuite de données | `Program.cs:158-162` (client) + `AgentOrchestratorService.cs:77-87` (fallback) |
| Cancellation utilisateur | jamais rejouée (OCE rethrow `ct.IsCancellationRequested`, timeouts dégradés proprement) | `ProfilerService.cs:149-166`, `SynthesizerAgent.cs`, `CheckerAgent.cs`, `WorkerExecutor.cs` |
| Corruptions d'état | index unique filtré (TOCTOU), migrations idempotentes | `20260731182015` |
| Streaming coupé | client conserve les tokens partiels, bouton « Réessayer » | `frontend/src/lib/api/chat.ts` |
| Erreurs internes | `UseExceptionHandler` en **premier** → JSON générique | `Program.cs:232-240` |

---

## 8. Migrations (SQL Server 2025)

| Migration | Contenu |
|---|---|
| `20260721103443_InitialCreate` | entités de base (`Embedding` en `varbinary(max)`) |
| `20260731134829_AddPayrollAndSalaryAdvance` | `PayrollProfile` + `SalaryAdvanceRequest` ; Embedding → **`vector(768)`** par `DropColumn`+`AddColumn` |
| `20260731162536_AddConversationHistory` | `AgentConversation`/`AgentMessage` |
| `20260731182015_AddUniquePendingSalaryAdvanceIndex` | index unique filtré `(EmployeeId) WHERE Status='Pending'` |
| `20260804095319_AddKnowledgeDocumentUniqueIndex` | index unique `(SourceFile, ChunkIndex)` — re-run d'ingestion sans `DELETE` **rejeté** |

> **Piège `vector`** : un `AlterColumn` de `varbinary` vers `vector` est **impossible** en SQL Server (conversion implicite interdite) → `DropColumn`+`AddColumn` + ValueConverter byte[]↔JSON (`AppDbContext.SerializeEmbedding`).

---

## 9. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0 | `00_README_INDEX` + `07_BUSINESS_DOMAIN` | ✅ |
| 1 | `01_CORE_ARCHITECTURE` (purge + notions/robustesse) | ✅ |
| 2 | `02_SECURITY_AND_RBAC` | ✅ |
| 3 | `03_AI_AND_RAG_PIPELINE` | ✅ |
| 4 | `04_QA_AND_TESTING` + `05` | ✅ |
| 5 | `06_Docker_Orchestration` | ✅ |
