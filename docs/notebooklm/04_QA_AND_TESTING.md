# AGIRH — QA & Tests (V6)

> **Objectif :** maîtriser la stratégie de test (122 tests), les dimensions couvertes et les commandes — ancré aux fichiers réels.
> **État :** `dotnet test -c Release` → **122/122** (0 failed, ~3-6 s).

---

## 1. Résumé exécutif (3 puces)

- **122 tests xUnit** (FluentAssertions + Moq + fakes maison) couvrant pipeline IA, RBAC/scope, TOCTOU, SSE, wire camelCase et la remédiation R1-R8.
- **V7 — retrait du `GreetingClassifier`** : `GreetingClassifierTests` (10) supprimés ; `AgentOrchestratorServiceTests` passe à **23** (flux greeting LLM, reformulation, fallback sans fuite, 6 préfixes verbatim, RoutingError, draft vide, bornage 500 + restauration, WIDGET sans-doublon/GUID strict, token replay) ; `AgentControllerDeniedTests` passe à **4** (sentinelle gated par `Outcome.Denied`). Détail : `AGIRH_MODEL_AUDIT.md` §5.
- **Frontend non couvert par `dotnet test`** : validé par les tests Sandbox manuels du runbook (vitest + RTL recommandés).

---

## 2. Inventaire des tests (122)

| Fichier | Compte | Couvre |
|---|---|---|
| `SalaryAdvanceIntegrationTests` | 16 | workflow avance, **cap 50 % (5000 ok / 5000.01 ko)**, TOCTOU, assertions exactes |
| `CheckerAgentTests` | 13 | **fail-closed** (5 chemins → false : HTTP non-2xx, contenu vide/nul, JSON introuvable, désérialisation null, exception), **R1** : écho doc long → invalide, budget `num_predict:256` + `think:false` racine + prompt strict |
| `AgentOrchestratorServiceTests` | 23 | flux greeting LLM (option c), reformulation `Unknown`, fallback sans fuite, 6 préfixes verbatim, RoutingError, draft vide, bornage 500 + restauration, WIDGET sans-doublon/GUID strict, SUGGEST-else, token replay, NotStreamed, sentinelle |
| `ZeroTrustDispatcherTests` | 11 | RBAC + scope (IDOR, manager, Admin exempt) |
| `ConversationsControllerTests` | 13 | historique, limites 3/5/7, IDOR 403, 404 |
| `SalaryAdvanceControllerTests` | 10 | GET `{id:guid}` anti-énumération (401/403/404) |
| `AgentControllerDeniedTests` | 4 | frame `denied` unique, jamais de `token` sentinelle, historique propre, **sentinelle non honorée hors `Outcome.Denied`** |
| `ProfilerServiceTests` | 8 | intention, `query` RAG, mapping Greeting, prompt Règles 1-7, **R2** : Règle 7 + garde mot-clé documentaire |
| `PipelineChainTests` | 3 | chaîne string→decimal + sentinelle |
| `UniquePendingIndexTests` | 2 | index filtré (metadata + SQL gardé) |
| `RbacMatrixTests` | 3 | mapping complet, **LeaveRequest → `PoserDemandeCongesAsync`** (≠ historique), GeneralInquiry→null |
| `PreFlightValidatorTests` | 6 | checklist catégorie (whitelist), pose de congés (date/jours) |
| `ChecklistFunctionsTests` | 3 | checklist vide exact, catégories listées, tri/format |
| `LeaveFunctionsTests` | 4 | création Pending+commit, IDOR collaborateur, date/jours invalides |
| Doubles | — | `PipelineFakes.cs`, `StubHttpMessageHandler.cs` |

> *(Le fichier `GreetingClassifierTests` — 10 tests — a été **supprimé** avec le classifieur.)*

---

## 3. Dimensions de test (méthodo)

| Dimension | Exemples AGIRH |
|---|---|
| **Happy path** | avance créée → succès + WIDGET ; login Admin → token |
| **Boundary** | 5000 € accepté / **5000.01 € refusé** ; limites conversations 3/5/7 |
| **Security** | RBAC déni (Collaborator→outil Admin), IDOR (avance d'autrui → 404), TOCTOU (2ᵉ Pending refusé) ; **R2** garde mot-clé documentaire → KnowledgeSearch ; **R5** pose de congés Pending + IDOR collaborateur |
| **Failure** | Ollama down → fallback ; réseau coupé → message générique ; config invalide → défauts ; **R1** écho du Checker → fail-closed ; **R4** « Aucune… » intercepté |

---

## 4. Commandes

| Action | Commande |
|---|---|
| Suite complète | `cd tests/Agirh.Tests; dotnet test -c Release` |
| Build Release | `dotnet build -c Release` (depuis `src/Agirh.Api`) |
| Filtre classe | `dotnet test -c Release --filter "FullyQualifiedName~Greeting"` (ProfilerServiceTests mapping Greeting + flux greeting orchestrateur) |
| Build frontend | `cd frontend; npm run build` (0 erreur) |

> **Piège Windows** : si `bin\Debug` est verrouillé par un process `Agirh.Api` en cours, utiliser `-c Release`.

---

## 5. Frontend — état & recommandation

| Point | État |
|---|---|
| Tests JS automatisés | ❌ aucun (validé par tests Sandbox manuels) |
| Module testable dès aujourd'hui | `frontend/src/lib/chat/markers.ts` (pur, 40 cas vérifiés) |
| Recommandé | **vitest + React Testing Library** : `markers.ts`, `SafeMarkdown` (XSS), store `ChatProvider` |

---

## 6. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0 | `00` + `07` | ✅ |
| 1 | `01_CORE_ARCHITECTURE` | ✅ |
| 2 | `02_SECURITY_AND_RBAC` | ✅ |
| 3 | `03_AI_AND_RAG_PIPELINE` | ✅ |
| 4 | `04_QA_AND_TESTING` (+ `05` polish) | ✅ |
| 5 | `06_Docker_Orchestration` (recentrage) | ✅ |
