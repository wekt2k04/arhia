# AGIRH — Les 3 fondations d'un système d'IA sécurisé

> **Cours de préparation** · Niveau : ingénieur junior · Format dense : chaque ligne = une notion.
> Les faits cités correspondent au code actuel (fichiers, classes, endpoints). 3 blocs indépendants mais complémentaires.

---

## 🧱 BLOC 1 — Architecture & Conception

> Objectif : structurer un système *pur*, *découplé*, *testable*.

### 1.1 Architecture hexagonale (Ports & Adapters)

**Notion.** Le **cœur métier** ne connaît pas la technique (BDD, HTTP, IA). Il expose des **ports** (interfaces) ; les **adaptateurs** (implémentations) sont branchés de l'extérieur. Règle d'or : **toute dépendance pointe vers l'intérieur** (le Domain ne dépend de rien).

**Dans AGIRH** — 4 projets .NET + 1 frontend :

| Couche | Contenu | Dépend de | Ne référence jamais |
|---|---|---|---|
| `Agirh.Domain` (pur) | Entités : `Employee`, `LeaveRequest`, `SalaryAdvanceRequest`, `PayrollProfile`, `ChecklistItem`, `KnowledgeDocument`, `AgentConversation` | rien (0 dép) | — |
| `Agirh.Core` | Ports du pipeline, `RbacMatrix`, use case `CreateSalaryAdvanceRequest`, `AIOptions` | Domain | Api, Infrastructure |
| `Agirh.Infrastructure` | `AppDbContext`, repos, `UnitOfWork`, agents IA, MAF tools, `JwtTokenService`, `OllamaEmbeddingGenerator` | Domain, Core | Api |
| `Agirh.Api` (composition root) | Contrôleurs, DTO, `Program.cs` | Domain, Core, Infra | — |
| `frontend/` | Next.js App Router + **BFF** | Api (HTTP via BFF) | Domain, Core, Infra |

**Mécanisme clé : inversion de dépendances (DIP).** Core déclare `ILeaveRequestRepository`, `IJwtTokenService`, `IZeroTrustDispatcher`… ; Infrastructure les implémente ; `Program.cs` branche impl → port au démarrage. Résultat : remplacer SQLite→SQL Server ou un LLM→un autre **sans toucher au métier**, et tester le cœur **sans base ni réseau**.

### 1.2 SOLID appliqué

- **DIP** : un contrôleur dépend de `ILeaveRequestRepository`, jamais du concret.
- **OCP (exemple remarquable)** : le pipeline découvre les **outils MAF** (`IMafTool`) **par réflexion** dans `Program.cs` (scan d'assembly). **Ajouter un outil = créer 1 classe = 0 modification du routeur.**
- **ISP** : chaque agent IA a son propre port fin (`ICognitiveProfiler`, `ISynthesizerAgent`, `ICheckerAgent`, …) — personne n'hérite de méthodes inutiles.

### 1.3 DDD léger : entités, invariants, use cases

**Notions.** Entité = identité + règles. Invariant = règle toujours vraie. Use case = action métier sans technologie.

**Le « fil rouge » à comprendre** — l'avance sur salaire porte ses invariants sur l'entité :
- cap : `AmountRequested ≤ NetSalary × MaxAdvancePercentage` (**50 %** → 5000 € pour Jean) ;
- unicité : **au plus un `Pending` par employé**.

Le use case `CreateSalaryAdvanceRequest` (dans `Core`, pur) applique ces règles **avant** toute persistance ; le contrôleur n'écrit aucune règle métier.

### 1.4 Composition root, BFF, UoW, lifetimes

- **Composition root** = unique endroit où l'on assemble le graphe (`Program.cs`). **Lifetimes** : `Scoped` = par requête · `Transient` = à chaque résolution (`AddHttpClient`) · `Singleton` = global.
- **BFF (Backend for Frontend)** : couche serveur Next.js entre navigateur et API, qui détient le **JWT en cookie httpOnly**. Le navigateur ne parle jamais à l'API directement.
- **UoW (Unit of Work)** : un contexte pour plusieurs écritures → **commit atomique** ; `AsNoTracking` pour les lectures pures.

### 1.5 Persistance : EF Core, migrations, `vector(768)`

- **5 migrations** : `InitialCreate` → `AddPayrollAndSalaryAdvance` (**Embedding → `vector(768)`** par `DropColumn`+`AddColumn`) → `AddConversationHistory` → `AddUniquePendingSalaryAdvanceIndex` (**anti-TOCTOU**, `(EmployeeId) WHERE Status='Pending'`) → `AddKnowledgeDocumentUniqueIndex`.
- **ValueConverter** : le vecteur d'embedding est `byte[]` en mémoire, **`vector(768)` natif** en base (un `AlterColumn` `varbinary`→`vector` est impossible en SQL Server → `Drop`+`Add`).

> **À retenir** — métier au centre, technique à la périphérie, dépendances vers l'intérieur, une seule racine d'assemblage, frontend isolé par un BFF, base versionnée par migrations.

---

## 🧠 BLOC 2 — IA Agentique & RAG

> Objectif : comprendre pourquoi un LLM nu est dangereux et comment le **fiabiliser** (pipeline, RBAC, recherche sémantique, streaming).

### 2.1 Pourquoi multi-agents ?

Un LLM seul hallucine, ignore les droits et répond lentement. On découpe en **6 acteurs + 1 orchestrateur**, chacun avec une **nature** (LLM = probabiliste, C# = déterministe) :

```
 IN : message + userId + rôle + conversationId (+ historique ≤ 3 messages)
        │
        ▼
 ┌───────────────────────────────────────────────┐
 │ AgentController · C# / SSE                    │  JWT · rate-limit · frontière HTTP
 └──────────────────────┬────────────────────────┘
                        ▼
 ┌───────────────────────────────────────────────┐
 │ AgentOrchestratorService · C# (pilote)        │  séquence · court-circuits · bornage 500 mots
 └──────┬───────────────────────────────┬────────┘
        ▼                               │
 ┌──────────────────────────┐           │
 │ ① Profiler · LLM          │           │  boucle ≤ 2 si paramètres manquants
 │  IN: message + historique │           │
 │  OUT: intention · conf.   │           │
 │       entités · query RAG │           │
 └──────┬───────────────────┘           │
        ▼                               │
 ┌──────────────────────────┐           │
 │ ② PreFlightValidator · C# │  présence/whitelist des paramètres (2 passes max)
 └──────┬───────────────────┘           │
        ▼                               │
 ┌──────────────────────────────────────────────┐   refusé (RBAC ou scope)
 │ ③ ZeroTrustDispatcher + RbacMatrix · C#      │   ──► \x1fDENIED\x1f ──► SSE « denied »
 │  décision de droits : 0 LLM                   │       → bulle orange, PAS de « done »
 └──────┬────────────────────────────────────────┘
        ▼ (autorisé)
 ┌──────────────────────────┐
 │ ④ WorkerExecutor + outil  │  10 outils MAF découverts par réflexion
 │   MAF · C#                │  UserId = JWT (jamais l'IA)
 └──────┬───────────────────┘
        ▼
 ┌──────────────────────────┐
 │ ⑤ Synthesizer · LLM       │  [Actor] draft ≤ 2 phrases, ancré aux données
 └──────┬───────────────────┘
        ▼
 ┌──────────────────────────┐   ko ──► fail-closed : JAMAIS le draft
 │ ⑥ Checker · LLM           │  [Critic] 5 chemins → false
 └──────┬───────────────────┘
        ▼ (ok)
 marqueurs C# (||WIDGET|| / ||SUGGEST||) ──► Token Replay ~30 ms ──► SSE conversation/token/done
```

> Légende : `· LLM` = probabiliste · `· C#` = déterministe (0 LLM). Les salutations **passent par le pipeline LLM complet** (plus aucun court-circuit pré-LLM).

### 2.2 Modèles Ollama & budgets

| Modèle | Rôle | `temp` | `num_predict` | `format:"json"` | Remarque |
|---|---|---|---|---|---|
| `phi4-mini:3.8b` | Profiler (routeur) | **0** | **512** | actif | Règles 1-7, 9 few-shot |
| `phi4-mini:3.8b` | Synthesizer (reformule) | **0.3** | **160** | non | 2 phrases max, anti-hallucination |
| `qwen3.5:9b` | Checker (verdict) | **0** | **256** | **retiré** + `think:false` racine | le plus gros (Bureau) ; local = `phi4-mini:3.8b` |
| `embeddinggemma` | Embeddings RAG | — | — | — | **768 dimensions** |

> **`think:false` est envoyé au niveau RACINE de la requête `/api/chat`** (pas dans `options`) — placé dans `options`, un modèle reasoning (ex. `gemma4:12b`) l'ignore et consomme tout le budget `num_predict` en raisonnement → `message.content` vide → checker fail-closed systématique. C'est aussi appliqué au Profiler et au Synthesizer.

**Budgets LLM bornés** : pas de fine-tuning — comportement = prompt strict + hyperparamètres + garde-fous C# en aval.
**Config** : `MaxExtractionRetries` = 2 (borné ≥ 1) · `MaxReflectionLoops` = 1 en config, **défaut code = 2**.
**Profils de lancement** (`launchSettings.json`) : `Agirh_Bureau` = Development (Ollama entreprise `192.168.100.220`) · `Agirh_Maison` = « local » (Ollama `localhost:11434`) · Production : `AI:Endpoint` **requis** sinon refus au démarrage.

### 2.3 Les 6 acteurs du pipeline

| # | Acteur | LLM ? | IN → OUT | Conception |
|---|---|---|---|---|
| ① | `ProfilerService` | oui | message → intention (enum fermé), confiance, entités, `query` RAG | `format:"json"`, garde R2 (mot « règlement/charte/politique/procédure » → force `KnowledgeSearch`), sanitisation PII **avant** LLM |
| ② | `PreFlightValidator` | non | (outil, entités) → params validés / KO | whitelists par outil ; `amount` requis (avance), `query` requis (RAG), catégorie whitelistée |
| ③ | `ZeroTrustDispatcher` + `RbacMatrix` | non | intention → outil MAF ou refus | gates : confiance → IsActive → rôle → scope Manager → dispatch |
| ④ | `WorkerExecutor` + MAF tools | non | outil + params → `RawWorkerData` (chunks) | exceptions → **message générique** (`ex.Message` jamais exposé) |
| ⑤ | `SynthesizerAgent` | oui | `rawData` (≤ 500 mots) → draft ≤ 2 phrases | repli : retourne le `rawData` brut si panne |
| ⑥ | `CheckerAgent` | oui | draft → `EvaluationResult(IsValid, feedback)` | **5 chemins → false**, exemption Greeting |

### 2.4 RBAC déterministe — le point le plus important

**Notion.** Décider **qui a droit à quoi** doit être **déterministe** (jamais une probabilité LLM).

**Dans AGIRH.** `RbacMatrix.Default` = matrice unique (rôle × capacité). Le dispatcher vérifie rôle + scope. Refus → sentinelle `\x1fDENIED\x1f` → SSE `denied` → **bulle orange**. Scope (IDOR) : self OK · subordonnés pour le Manager · Admin exempt. **0 LLM dans la décision.**

**Les 10 outils MAF** (1 classe = 1 outil, OCP) :

| Outil | Rôles |
|---|---|
| `ConsulterSolde` / `ConsulterHistoriqueConges` / `PoserDemandeConges` / `DemanderAvanceSalaire` / `RechercherInformationRag` | Tous (self-only, gardes internes) |
| `GenererSoldeToutCompte` / `EnvoyerAlerteManager` / `GenererChecklist` / `ApprouverDemandeConges` | Admin \| Manager |
| `RevoquerAccesIT` | Admin |

### 2.5 Actor-Critic + fail-closed

- **Synthesizer = Actor** (produit), **Checker = Critic** (évalue). Draft invalide → 2ᵉ tour si budget → sinon message propre.
- **Fail-closed : 5 chemins → `EvaluationResult(false)`** : HTTP non-2xx · contenu vide/nul · JSON introuvable · désérialisation null · exception/timeout.
- **Bornage 500 mots** du `RawWorkerData` avant Synthesizer/Checker (réduit surface d'injection et écho).
- Fail-closed partout : `IsActive=false` par défaut à l'extraction, `FallbackPolicy` exige un JWT, Checker refuse par défaut.

### 2.6 Greetings & intercepteur d'erreurs

- **Salutations** : Règle 6 du Profiler → `Greeting` → `RbacMatrix.ResolveTool("Greeting") = null` → **GeneralChat** → Synthétiseur (salutation polie) → Checker (**exemption Greeting**).
- **Interception (0 LLM)** : les préfixes `Erreur` / `Accès refusé` / `Action non autorisée` / `Aucun` / `Aucune` / `L'opération a été annulée` produits par un outil sont renvoyés **verbatim**, skip synthèse/checker.

### 2.7 RAG — la réponse s'ancre sur des documents

1. **Ingestion (offline)** : fichiers `.md` → **chunking fenêtre 512 mots, chevauchement 128** → `embeddinggemma` (768 d, lots) → colonne **`vector(768)`** SQL Server 2025.
2. **Requête (online)** : le Profiler produit une `query` → `KnowledgeDocumentRepository` : `VECTOR_DISTANCE('cosine', Embedding, @query)` → **top-5 sémantique** (requête **paramétrée**, aucune injection SQL).
3. **Génération** : contexte injecté dans le Synthesizer → réponse **sourcée**. Panne embedding → réponse **dégradée**, jamais de fuite technique.

### 2.8 Streaming SSE & marqueurs

- **SSE** : événements `conversation` / `token` (~30 ms) / `denied` / `error` / `done`. **Token Replay** : `Split(' ')` + `Task.Delay(30 ms)` simule l'inférence côté client.
- **Marqueurs** : `||WIDGET:SalaryAdvance:{id}||` (carte suivi, généré dans le use case), `||SUGGEST:Poser un congé||` / `||SUGGEST:Suivre ma demande||` (chips). Regex GUID stricte, ré-apposé si perdu par le LLM, jamais dupliqué, strip `\x1f` pré-stream.
- Frontend : `markers.ts` (regex + `stripPartialMarkers` anti-flash) · `sanitizeSchema.ts` (liste blanche Markdown, zéro XSS).

> **À retenir** — séparer les rôles (pipeline), droits déterministes (C#), valider avant d'envoyer (Actor-Critic fail-closed), ancrer sur des documents (RAG), streamer (SSE), bornage systématique (500 mots).

---

## 🛡️ BLOC 3 — Sécurité, Qualité & Industrialisation

> Objectif : rendre le système *Zero-Trust*, *prouvé par les tests* et *déployable*.

### 3.1 Authentification : JWT + BCrypt + cookie httpOnly

| Élément | Valeur |
|---|---|
| JWT | **HS256**, clé ≥ 32 chars, **8 h** (clampé [1,24]) |
| Claims | `nameid`, `email`, `given_name`, `family_name`, `role`, `is_active`, `managerId`?, `exp` |
| Validation | issuer + audience + lifetime + signature, **ClockSkew = 1 min** |
| Mots de passe | **BCrypt** `$2a$11$` (vérification à temps constant) |
| Stockage | **cookie httpOnly** posé par le **BFF** → jamais visible du JS (anti-XSS) |

### 3.2 RBAC Zero-Trust + FallbackPolicy

- **FallbackPolicy** = `RequireAuthenticatedUser()` : **toute route oubliée est refusée, jamais ouverte** (secure-by-default). Publics **explicites** : `[AllowAnonymous]` sur login + health.
- **Rate-limit** (bucket + IP, fenêtre 1 min) : **login 5/min · chat 20/min · open 1000/min → 429**.

### 3.3 Menaces traitées

| Menace | Problème | Solution AGIRH |
|---|---|---|
| **IDOR** | accéder à la ressource d'autrui | scopes self / subordonnés / Admin (`EvaluateOwnerScope`) + gardes MAF |
| **TOCTOU** | course « vérifier puis agir » | **index unique filtré** `(EmployeeId) WHERE Status='Pending'` + garde app = **double défense** |
| **Anti-énumération** | révéler l'existence d'une ressource | **404 seul** (corps vide) pour toute demande invisible |
| **Bruteforce** | marteler le login | rate-limit 5/min → **429** |
| **XSS** | script via Markdown | liste blanche de tags, zéro `dangerouslySetInnerHTML` |
| **Fuites d'erreurs** | exposer les détails internes | `ex.Message` jamais renvoyé ; audit tronqué à **400 chars** |

**TOCTOU, cas emblématique** : 2 avances concurrentes passent le *check* « un seul Pending » puis écrivent → la base **rejette** la 2ᵉ (index filtré). Garde app + garde base.

### 3.4 Robustesse : Polly

- **Timeouts bornés** : health 15 s · Profiler 300 s (600 s en Development, 30 s en Production) · Synthèse/Checker 120 s · **Embedding 180 s**.
- **Retry** : `WaitAndRetryAsync(3)` backoff **200/400/800 ms**. **La cancellation utilisateur n'est jamais rejouée** (on ne retente que les vrais timeouts).
- Ollama down → fallback `AI_UNAVAILABLE` → message générique (jamais de fuite de données).

### 3.5 Tests : la preuve

**122 tests** (108 `[Fact]` + 14 cas `[Theory]`/`[InlineData]`) · xUnit + Moq + FluentAssertions · **4 dimensions** :

| Dimension | Exemple |
|---|---|
| Happy path | avance acceptée, WIDGET émis |
| Boundary | **5000 € ok / 5000,01 € refusé** |
| Security | refus RBAC Collaborateur sur outil Admin, TOCTOU |
| Failure | Ollama down → fallback sans fuite |

Grâce à l'hexagonale, les tests tournent **sans serveur ni base réelle**.

### 3.6 Déploiement : Docker

- **SQL Server 2025** conteneurisé (image `2025-latest`, port `1433`, `docker/sql/Dockerfile`).
- **API** (.NET 8) et **frontend** (Next.js) lancés **nativement** (`dotnet run`, `npm run dev`).
- **Ollama** joint par endpoint direct (`AI:Endpoint`).
- **Secrets** par variables d'environnement (`MSSQL_SA_PASSWORD`, `Jwt__Key`) — jamais dans l'image ni `appsettings` (vidé/gitignoré).

### 3.7 Endpoints & comptes seed

| Endpoint | Accès | Comportement |
|---|---|---|
| `/api/auth/login` · `/api/health` · `/api/agent/health` | public | `[AllowAnonymous]` explicites |
| `/api/agent/chat` | authentifié | SSE `conversation/token/denied/error/done` |
| `/api/conversations` | authentifié | historique limité **3/5/7** (Collaborator/Manager/Admin) |
| `/api/salary-advance/{id:guid}` | authentifié | **200** propriétaire · **404** sinon (anti-énumération) |
| `/api/admin/ingest` | **Admin** | ingestion RAG |

**Comptes seed (développement uniquement)** : `admin@agirh.fr/admin123` (Admin) · `marie.martin@agirh.fr/manager123` (Manager, 25 j) · `jean.dupont@agirh.fr/collab123` (Collaborator, 15 j, payroll `NetSalary=10000` / cap 50 %) · `pierre.durand@agirh.fr/collab123` (**inactif**). Checklist seed : **11 items** (IT 3, Administratif 3, RH 3, Management 2).

### 3.8 Logging & audit

- **FileLogger** → `logs/agirh-api.log` · **ChatAuditLogger** → `logs/agirh-audit.jsonl` — les 2 **réinitialisés à chaque démarrage** ; `NoopChatAuditLogger` en secours si échec d'init.
- Audit JSONL par chat : rôle, modèles, `ReflectionLoops`, `CheckerValid`, `WidgetId`, `Suggestion`, `Outcome` ; Message/Response tronqués **400 chars**, log Checker **300 chars** (PII indirecte).

> **À retenir** — refuser par défaut (FallbackPolicy, fail-closed), défendre en profondeur (garde app + index base), ne rien exposer d'existant (404), prouver par 122 tests, industrialiser (SQL conteneurisé, secrets en env), tout tracer (audit).

---

## 📚 Glossaire FR ↔ EN

| Terme | English | Définition courte |
|---|---|---|
| Architecture hexagonale | Hexagonal / Ports & Adapters | Cœur métier isolé, implémentations branchées à la périphérie |
| Port / Adapter | Port / Adapter | Interface du cœur / implémentation concrète (EF, Ollama, JWT…) |
| Use case | Use case | Action métier exprimée sans technologie |
| DIP | Dependency Inversion | Dépendre des abstractions, pas des classes |
| Composition root | Composition root | Endroit unique d'assemblage du graphe d'objets |
| BFF | Backend for Frontend | Couche serveur qui détient les secrets (JWT) côté navigateur |
| UoW | Unit of Work | Regroupe des écritures en transaction atomique |
| Actor-Critic | Actor-Critic | Un producteur + un validateur de réponse |
| Fail-closed | Fail-closed | En cas de doute : refuser, jamais ouvrir |
| RAG | Retrieval-Augmented Generation | Réponse générée à partir de documents récupérés |
| Chunking | Chunking | Découpage d'un document (512/128) |
| Embedding / vector(768) | Embedding / vector(768) | Vecteur numérique de sens, stocké en type SQL natif |
| TOCTOU | Time-of-check Time-of-use | Course « vérifier puis agir » |
| IDOR | Insecure Direct Object Reference | Accès à la ressource d'autrui par id |
| Anti-énumération | Enumeration | Ne pas révéler l'existence d'une ressource |
| RBAC / Zero-Trust | RBAC / Zero-Trust | Contrôle par rôle / aucune confiance par défaut |
| SSE | Server-Sent Events | Poussée d'événements serveur → navigateur |
| Token Replay / Marqueur | Token Replay / Marker | Renvoi de jetons au fil du streaming / balise IA (widget/chip) |
| ValueConverter | Value Converter | Transformation de type à la persistance (byte[] ↔ vector) |
| Retry / backoff | Retry / backoff | Réessai avec délais croissants (200/400/800 ms) |

---

## 🎯 Synthèse : un seul système, toutes les notions

Une **avance sur salaire demandée dans le chat** traverse tout : la requête entre par `AgentController` (**JWT** validé, **rate-limit**, **composition root**), via le **BFF** qui a lu le **cookie httpOnly** (jamais dans le JS). L'**orchestrateur** envoie le message au **Profiler** (intention + `query` RAG, sanitisation PII), puis au **PreFlightValidator** et au **ZeroTrustDispatcher** qui tranchent **en C#** (matrice unique, fail-closed, scope **anti-IDOR**) — sinon sentinelle → SSE `denied`. Autorisé, le **Worker** exécute l'outil MAF qui appelle le **use case pur** du Domain : cap 50 % (boundary 5000/5000,01) et unicité du Pending (invariant), défendu en profondeur par l'**index unique filtré anti-TOCTOU**. Le **Synthesizer** reformule (2 phrases, bornage 500 mots), le **Checker** valide (Actor-Critic, **5 chemins fail-closed**), le tout enrichi du contexte **RAG** (`vector(768)`, `VECTOR_DISTANCE` top-5) puis streamé en **SSE** ~30 ms/token avec le marqueur `||WIDGET||` → carte dans le chat. Tenue debout par **Polly** (timeouts, cancellation jamais rejouée), prouvée par **122 tests** (4 dimensions) grâce au découplage hexagonal, tracée par l'**audit JSONL**, avec **SQL conteneurisé** (Docker) et secrets par env. Chaque notion — architecture, IA, sécurité — n'est pas une option mais une brique d'un même édifice : **le métier au centre, l'IA encadrée, la confiance prouvée**.
