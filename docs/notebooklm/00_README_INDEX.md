# AGIRH — Index & Carte des Notions (V6)

> **Objectif :** maîtriser la **logique métier** + les **notions/outils/patterns** du projet AGIRH.
> **Format :** chaque doc = résumé exécutif ≤ 3 puces + tableaux + encadrés + **gras** sur mots-clés, **ancré au code** (fichier/classe/endpoint), tableau de bord en fin.

---

## 1. Résumé exécutif (3 puces)

- **AGIRH = assistant RH agentique** : backend .NET 8 hexagonal (Domain/Core/Infrastructure/Api) + frontend Next.js 15, pipeline IA **6 acteurs** (Profiler → PreFlight → Dispatcher RBAC → Worker → Synthesizer → Checker fail-closed), RBAC fail-closed, streaming SSE.
- **Lire dans l'ordre :** `07` (métier) → `01` (architecture) → `02` (sécurité) → `03` (IA/RAG) → `05` (frontend) → `04` (tests) → `06` (Docker).
- **État :** suite backend **122/122**, build frontend 0 erreur, audits GO (V5 backend) + remédiation modèle **R1-R8** (`AGIRH_MODEL_AUDIT.md`).

---

## 2. Carte des fichiers

| Doc | Contenu | Lecture pour |
|---|---|---|
| `07_BUSINESS_DOMAIN` | Rôles/hiérarchie, congés/CET, avance, onboarding, workflows | Maîtriser le **métier** |
| `01_CORE_ARCHITECTURE` | Hexagonal, entités, ports, DI, migrations, **patterns & robustesse** | Comprendre l'**architecture** |
| `02_SECURITY_AND_RBAC` | JWT/BCrypt/cookie, RBAC, rate-limit, IDOR/TOCTOU/anti-énumération, `denied` | Comprendre la **sécurité** |
| `03_AI_AND_RAG_PIPELINE` | 6 acteurs du pipeline, fail-closed, RAG/vector(768), SSE/marqueurs, Polly | Comprendre l'**IA/RAG** |
| `05_FRONTEND_AND_DEPLOYMENT` | Next.js, BFF, cookie httpOnly, streaming, sanitization | Comprendre le **frontend** |
| `04_QA_AND_TESTING` | 122 tests, dimensions happy/boundary/security/failure | Maîtriser les **tests** |
| `06_Docker_Orchestration_AGIRH` | Dockerfile SQL, API/frontend natifs, pont Ollama, secrets | Comprendre le **déploiement** |

---

## 3. Carte notion → fichier → code → comportement

| Notion / Pattern | Où l'apprendre | Ancrage code | Comportement clé |
|---|---|---|---|
| **Hexagonal / Ports & Adapters** | `01` | `Domain/Interfaces/*`, `Core/Interfaces/*` | Domain 0 dép ; adaptateurs en Infrastructure |
| **OCP (découverte dynamique MAF)** | `01` | `Program.cs:141-148` | Ajouter un outil = 1 classe `IMafTool`, zéro modif router |
| **DIP** | `01` | `IJwtTokenService` (Core), `OllamaEmbeddingGenerator` (Infra) | Les contrôleurs dépendent des ports, pas des implémentations |
| **UoW / Repositories** | `01` | `UnitOfWork.cs`, `Repositories/*` | Persistance atomique, `AsNoTracking` en lecture |
| **RBAC (source unique)** | `02` | `RbacMatrix.Default.ResolveTool`, `ZeroTrustDispatcher.cs:78-129` | 1 seule matrice, dispatch fail-closed |
| **IDOR / Scope** | `02` | `EvaluateOwnerScope` (`:165-175`), gardes MAF | self OK, autre employé refusé sauf Admin |
| **Anti-énumération** | `02` | `SalaryAdvanceController.cs:52-62` | **404 seul** (corps vide) ; message « Demande introuvable ou inaccessible » normalisé côté frontend |
| **TOCTOU** | `02` | migration `20260731182015` (index filtré) + garde app | 1 seul `Pending` par employé |
| **Fail-closed** | `02`+`03` | `CheckerAgent.cs:76-102`, extraction claim absent → `false` (`HardStateExtractor.cs:30`) | Toute erreur = refus, jamais d'accès |
| **Rate-limiting** | `02` | `Program.cs:94-125` | login 5/min, chat 20/min, 429 |
| **FallbackPolicy secure-by-default** | `02` | `Program.cs:87-92` | Toute route exige un JWT sauf `[AllowAnonymous]` |
| **Salutations** | `03` | `ProfilerService` (Règle 6 → `Greeting`) | « Bonjour » → LLM → GeneralChat → synthétiseur → checker |
| **Actor-Critic** | `03` | `SynthesizerAgent` + `CheckerAgent` | Draft reformulé puis validé (rejet si invalide) |
| **RAG / embedding / cosine** | `03` | `RagFunctions.cs`, `KnowledgeDocumentRepository.cs` | Top-5 sémantique, `VECTOR_DISTANCE` |
| **vector(768) + ValueConverter** | `03` | `AppDbContext.SerializeEmbedding`, migration `20260731134829` | byte[]↔JSON (pas d'`AlterColumn` possible) |
| **SSE / Token Replay** | `03`+`05` | `AgentController.cs:115-177`, `AgentOrchestratorService` | tokens ~30 ms, événements `conversation/token/denied/error/done` |
| **Marqueurs IA** | `03`+`05` | `CreateSalaryAdvanceRequest.cs:78`, `lib/chat/markers.ts` | `||WIDGET:…||` carte, `||SUGGEST:…||` chips |
| **JWT / BCrypt** | `02` | `JwtTokenService.cs`, `AuthController.cs` | HS256 8h, claims standard, hash $2a$11$ |
| **Cookie httpOnly (BFF)** | `02`+`05` | `frontend/src/lib/auth/cookies.ts` | Token jamais dans le JS |
| **Polly (résilience)** | `01`+`03` | `Program.cs:158-214` | Retry 3 (200/400/800 ms), timeout embed 180 s |
| **Streaming par lots (UX)** | `05` | `frontend/src/lib/api/chat.ts` | ≤ 10 re-rendus/s au lieu de 33 |
| **Sanitization XSS** | `05` | `lib/markdown/sanitizeSchema.ts` | Liste blanche, strip script/img/svg |
| **bUnit / tests (à venir)** | `04` | — | Frontend validé par tests Sandbox manuels |

---

## 4. Méthode d'étude conseillée

1. **Parcours métier** : lire `07` → savoir expliquer un workflow (congé, avance, déni) sans code.
2. **Parcours architecture** : `01` → `02` → `03` (chaque notion = comprendre « pourquoi ce choix » + « où dans le code »).
3. **Parcours exécution** : `05` → `04` → `06` (comment ça tourne, comment c'est testé, comment c'est déployé).
4. **Auto-validation** : exécuter `AGIRH_SIMULATION_RUNBOOK.md` (Partie A puis B) ; chaque critère a un signe OK/KO.
5. **QCM mental** : reprendre la carte §3 et reformuler chaque ligne en une phrase.

---

## 5. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0 | `00_README_INDEX` + `07_BUSINESS_DOMAIN` | ✅ |
| 1 | `01_CORE_ARCHITECTURE` (purge + notions/robustesse) | ✅ |
| 2 | `02_SECURITY_AND_RBAC` (purge + notions) | ✅ |
| 3 | `03_AI_AND_RAG_PIPELINE` (réécriture réelle) | ✅ |
| 4 | `04_QA_AND_TESTING` (purge) + `05` (polissage) | ✅ |
| 5 | `06_Docker_Orchestration` (recentrage) + liens croisés | ✅ |
