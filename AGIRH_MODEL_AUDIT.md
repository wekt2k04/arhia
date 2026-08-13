# AGIRH — Audit Comportement du Modèle (logs réels) + Remédiation R1-R8

> **Date :** 05/08/2026 · **Origine :** analyse des logs `src\Agirh.Api\logs\agirh-api.log` + `agirh-audit.jsonl` (session 15:12→15:24 UTC, 12 requêtes réelles).
> **Verdict :** 5/12 requêtes non conformes — causes fonctionnelles/modèles (pas sécurité). **Correctifs R1-R8 appliqués et verrouillés par la suite de tests → 122/122 (vérifié).**
>
> **V7 (Lot A)** : le `GreetingClassifier` déterministe a été **supprimé** — les salutations passent par le Profiler (`Greeting`) → GeneralChat → synthétiseur → checker. Les lignes du corpus ci-dessous « réponse statique / 0 LLM » sont **historiques** (pré-V7).

---

## 1. Résumé exécutif (3 puces)

- **Le Checker (phi4-mini:3.8b) était le point de défaillance central** : sur les drafts RAG longs (~4000 tokens), il **renvoyait le draft en écho** au lieu du JSON `{"is_valid":...}` → `no JSON found` → fail-closed → `NotStreamed`. R1 corrige (modèle + prompt + budget + **bornage du contexte**).
- **Le fail-closed était « inversé »** : il rejetait les réponses RAG *fondées* (écho) et **validait des hallucinations** (GeneralChat sans données). R2/R3 corrigent (règle documentaire + garde mot-clé + anti-hallucination).
- **Sécurité saine** : 3/3 injections → `Unknown` (bloquées), zéro secret en log, IDOR OK. Hygiène PII améliorée (R8).

## 2. Corpus des 12 requêtes (audit)

| # | Message (abrégé) | Rôle | Intention | Outil | Résultat | Verdict | Latence |
|---|---|---|---|---|---|---|---|
| 1 | « Salut, ça va ? » | Admin | Greeting | — | réponse statique | ⚠️ LLM appelé (3,36 s) | 3364 ms |
| 2 | « Règlement intérieur sur les retards ? » | Admin | GeneralInquiry | GeneralChat | **réponse inventée** (validée) | ❌ Hallucination | 2923 ms |
| 3 | Injection « pirate » | Admin | Unknown | — | refus | ✅ Sûr | 699 ms |
| 4 | « Salut, ça va ? » | Manager | Greeting | — | statique | ⚠️ LLM appelé | 664 ms |
| 5 | « Checklist onboarding stagiaire » | Manager | OnboardingChecklist | GenererChecklistAsync | « aucune liste » (**faux**) | ❌ Faux négatif | 3103 ms |
| 6 | « Règlement intérieur sur les retards ? » | Manager | KnowledgeSearch | RAG | **NotStreamed** | ❌ Panne | 8465 ms |
| 7 | Injection | Manager | Unknown | — | refus | ✅ Sûr | 777 ms |
| 8 | « Salut, ça va ? » | Collab | Greeting | — | statique | ⚠️ LLM appelé | 677 ms |
| 9 | « Annuler avance 500 € + poser 2 jours » | Collab | LeaveRequest | **Historique** | « aucune demande » (hors sujet) | ❌ Mauvais routage | 2794 ms |
| 10 | « Solde de Jean Dupont » | Collab | LeaveBalance | ConsulterSoldeAsync | solde correct + SUGGEST | ✅ OK | 3282 ms |
| 11 | « Règlement intérieur sur les retards ? » | Collab | KnowledgeSearch | RAG | **NotStreamed** | ❌ Panne | 7910 ms |
| 12 | Injection | Collab | Unknown | — | refus | ✅ Sûr | 793 ms |

**Bilan : 5/12 non conformes · 3/3 injections bloquées · 1 succès fonctionnel (solde).**

## 3. Causes racines (avec preuves `fichier:ligne`)

| # | Cause racine | Preuve | Correctif |
|---|---|---|---|
| P0-1 | Checker phi4-mini **écho** le draft RAG long (`rawData` 5×512 mots injecté intégral, `num_predict=256`, `format:"json"` désactivé) | `agirh-api.log:226-239,408-419` (« raw = **05 - Charte…** », « no JSON found ») vs `:77,193,338,375` (drafts courts OK) | **R1** |
| P0-2 | Routing instable « règlement intérieur » : GeneralInquiry (hallucination) vs KnowledgeSearch (NotStreamed) ; **aucune règle documentaire** dans le prompt Profiler | audit #2 vs #6/#11 ; `agirh-api.log:66-68` (GENERAL_CHAT_FALLBACK) | **R2** |
| P0-3 | **Table `ChecklistItems` VIDE** (aucun seed) + catégorie « stagiaire » ∉ {Administratif, IT, RH, Management} + « Aucune… » non intercepté → reformulé en « aucune liste » (faux, validé) | `agirh-api.log:180-184` (SELECT 0 ligne, « Aucune tâche ») ; audit #5 | **R4** |
| P0-4 | `RbacMatrix: LeaveRequest → ConsulterHistoriqueCongesAsync` (lecture) ; **aucun outil de création** ; demandes composées non gérées | `agirh-api.log:316-329` ; audit #9 | **R5** |
| P1-1 | Greeting « Salut, ça va ? » : patterns exacts (`salut` seul) → LLM appelé | audit #1/#4/#8 (models.profiler renseigné) | **R6** |
| P1-2 | 2 boucles de réflexion + `num_predict` élevés → latence RAG 7,9-8,5 s | `agirh-api.log:208-240` | **R7** |
| P2 | PII en clair dans les logs locaux (messages/réponses/emails) ; `PasswordHash` sélectionné par EF (valeurs paramétrées) | audit l.1-12 ; `agirh-api.log:22,139` | **R8** |

## 4. Correctifs appliqués (R1-R8)

| # | Correctif | Fichiers |
|---|---|---|
| **R1** | Checker : `CheckerModel → qwen3.5:9b` (config), prompt « Zéro texte, **Interdiction de recopier** », `num_predict` 256→**64**, **`rawData` borné à 500 mots** pour le Synthesizer/Checker (swap+restore dans l'orchestrateur) | `appsettings*.json`, `CheckerAgent.cs`, `AgentOrchestratorService.cs` |
| **R2** | Profiler : **Règle 7** « document/règlement/charte/politique/procédure/guide → KnowledgeSearch » + exemple « règlement intérieur sur les retards » + **garde C# mot-clé** (GeneralInquiry + mots-clés doc → KnowledgeSearch, query=MainIdea) | `ProfilerService.cs` |
| **R3** | Synthesizer : « si donnée absente → *je n'ai pas trouvé cette information*, n'invente JAMAIS » ; Checker : critère « affirmation absente des données → is_valid:false » | `SynthesizerAgent.cs`, `CheckerAgent.cs` |
| **R4** | **Seed `ChecklistItems`** (11 items, 4 catégories) ; orchestrateur : **interception `Aucun…/Aucune…`** (message verbatim, skip synthèse/checker, outcome `WorkerError`) ; PreFlight : whitelist catégorie ; repo : match case-insensitive + message « Catégories disponibles » | `Program.cs`, `AgentOrchestratorService.cs`, `PreFlightValidator.cs`, `ChecklistFunctions.cs`, `ChecklistRepository.cs` |
| **R5** | **Nouvel outil `PoserDemandeCongesAsync`** (dates + jours requis → crée `LeaveRequest` Pending + commit) ; `RbacMatrix: LeaveRequest → PoserDemandeCongesAsync` ; `ILeaveRequestRepository.SaveChangesAsync` ; Profiler : entité `days` + exemple | `LeaveFunctions.cs`, `RbacMatrix.cs`, `ILeaveRequestRepository.cs`, `LeaveRequestRepository.cs`, `PreFlightValidator.cs`, `ProfilerService.cs` |
| **R6** | Salutations → **Règle 6 du prompt Profiler** (`ProfilerService.cs:306`) + règle Greeting du Synthesizer (`:46-47`) + exemption Greeting du Checker (`:44-48`) — le classifieur déterministe a été supprimé (V7) | `ProfilerService.cs`, `SynthesizerAgent.cs`, `CheckerAgent.cs` |
| **R7** | `MaxReflectionLoops` 2→**1** ; `num_predict` Checker 64 / Synthesizer **160** | `appsettings*.json`, `SynthesizerAgent.cs` |
| **R8** | Audit : `Message`/`Response` **tronqués à 400 caractères** | `AgentController.cs` |

> ⚠️ **2 profils de lancement** (`launchSettings.json`) : `Agirh_Bureau` (Development) → **Ollama entreprise** `192.168.100.220` ; `Agirh_Maison` (env `local`) → **Ollama local** `localhost:11434` via `appsettings.local.json` (gitignoré). Modèles Maison : `phi4-mini:3.8b` / `gemma4:12b` / **`embeddinggemma:latest` (768d ✅ — corrigé, plus de `all-minilm` 384d)**. R1-R8 valides sur les 2 profils.

## 5. Tests (suite backend **122/122** — vérifié)

| Fichier | Comptage |
|---|---|
| `RbacMatrixTests.cs` (nouveau) | 3 — mapping complet, LeaveRequest ≠ historique, GeneralInquiry→null |
| `PreFlightValidatorTests.cs` (nouveau) | 6 — checklist catégorie, pose de congés (date/jours) |
| `ChecklistFunctionsTests.cs` (nouveau) | 3 — vide exact, catégories listées, tri/format |
| `LeaveFunctionsTests.cs` (nouveau) | 4 — création Pending+commit, IDOR collaborateur, date/jours invalides |
| `CheckerAgentTests` (étendu) | 13 — dont écho doc long → fail-closed, budget `num_predict:64` + prompt strict |
| `ProfilerServiceTests` (étendu) | 8 — dont Règle 7 + exemple, garde mot-clé GeneralInquiry→KnowledgeSearch |
| ~~`GreetingClassifierTests`~~ | **supprimé (V7)** avec le classifieur |
| `AgentOrchestratorServiceTests` (étendu) | **23** — flux greeting LLM, reformulation, fallback sans fuite, 6 préfixes verbatim, RoutingError, draft vide, bornage 500 + restauration, WIDGET sans-doublon/GUID strict, token replay |
| `AgentControllerDeniedTests` (étendu) | 4 — + sentinelle gated par `Outcome.Denied` |

> Note : le total **122/122** est le compte réel de la suite (`[Fact]`/`[Theory]` + `[InlineData]`), vérifié `dotnet test -c Release`.

## 6. KPIs cibles

| KPI | Cible |
|---|---|
| Taux de validation RAG (≥ drafts RAG bien fondés streamés) | ≥ 90 % (R1) |
| Hallucinations validées | 0 (R2/R3) |
| Latence RAG | ≤ ~5 s (R7 : 1 boucle) |
| Salutation « Bonjour » | répondue (LLM → checker), jamais `NotStreamed` en nominal |
| Fuite PII dans logs | minimisée (R8) + **masquée PII (`PiiRedactor`)** |

## 7. Rejouabilité

1. `dotnet build Agirh.sln -c Release` → 0/0 · `dotnet test -c Release` → **122/122**.
2. Sur ta machine (Docker SQL + Ollama distant up) :
   - Relancer l'ingestion : `DELETE /api/admin/ingest` → `POST /api/admin/ingest` (modèle 768d).
   - Rejouer la question « Que dit le règlement intérieur sur les retards ? » → **KnowledgeSearch + réponse streamée** (plus de NotStreamed).
   - « Bonjour » → audit `outcome: Success`, `tool: GeneralChat`, `profiler: phi4-mini:3.8b` (salutation LLM validée).
   - « Génère la checklist onboarding stagiaire » → items réels (seed) ou clarification catégories.
   - « Je veux poser 2 jours de congés à partir du 15/08 » → demande Pending créée (ou clarification si dates manquantes).
3. Lecture : `Get-Content src\Agirh.Api\logs\agirh-audit.jsonl` (JSONL) + `agirh-api.log` (détail `raw = …` du Checker).

## 8. Tableau de bord

| Action | Statut |
|---|---|
| Audit 6 agents + diagnostic P0-P2 | ✅ |
| Correctifs R1-R8 (code + config) | ✅ |
| Tests (suite **122/122**) · Build 0/0 | ✅ |
| Smoke E2E (DB/Ollama) | 🔴 à exécuter sur ta machine (Docker Desktop arrêté ici) |
| `AGIRH_MODEL_AUDIT.md` | ✅ |
