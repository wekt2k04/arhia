# État du projet — arhia V8

*Réécrit le 2026-08-30 — la version précédente décrivait l'état des milestones 3-4 (seul
`AuthController` existait) et était devenue significativement obsolète : le socle métier, le
pipeline RAG, l'orchestration conversationnelle et le frontend sont depuis tous construits et
vérifiés. Voir `.claude/HANDOFF/NEXT_SESSION.md` pour le détail narratif à jour, ce fichier reste
un résumé factuel condensé pour orienter les agents custom.*

Le projet a été **entièrement remis à zéro** le 2026-08-14 (V7 → V8, voir `docs/HISTORIQUE.md` à la
racine). Le code V7 est supprimé (récupérable via le tag git `v7-archive` si besoin). Toute règle
qui cite un fichier/chemin V7 (`Profiler`, `Synthesizer`, `Checker`, `ZeroTrustDispatcher`,
`AgentOrchestratorService`, RBAC à rôles `Admin/Manager/Collaborator`) doit être traitée comme
obsolète.

## Specs canoniques (sous `docs/` à la racine du repo)
1. **`docs/LOGIQUE_METIER.md`** — rôles, workflow onboarding/offboarding, RBAC, garde-fous IA. **Fait.**
2. **`docs/STACK_TECHNIQUE.md`** — stack backend/frontend/données/IA. **Fait.**
3. **`docs/ARCHITECTURE.md`** — hexagonal, arborescence de dossiers, diagrammes. **Fait.**
4. **`docs/CHECKLIST.md`** — suivi milestone par milestone (statuts en émojis, livré, reste à faire). Mis à jour à la fin de chaque étape.
5. **`.claude/HANDOFF/NEXT_SESSION.md`** + **`.claude/HANDOFF/LOG.md`** — protocole de continuité multi-appareils (PC ↔ mobile), décrit dans `CLAUDE.md` à la racine. Une session fraîche (y compris ces agents custom) doit s'y référer pour l'état courant plutôt que de le redécouvrir.

## Code existant (vérifié le 2026-08-30 — presque tous les milestones 0-11 sont livrés)

**Backend** (`Arhia.sln`, .NET 8) : `Arhia.Domain`, `Arhia.Core`, `Arhia.Infrastructure`,
`Arhia.Api`, `Arhia.Tests`. **245 tests hors catégorie `Evaluation`** (244 verts, 1 flake pré-
existant sans rapport — `RagPipelineIntegrationTests`, accumulation Qdrant persistante entre
exécutions), 0 warning au build — reconfirmé le 2026-08-30 par une **exécution réelle**
(`dotnet test Arhia.sln -c Release --filter "Category!=Evaluation"`). **Piège rencontré ce même
jour** : `dotnet test --list-tests` compté via un grep naïf avait sous-compté à 211 (les tests
`[Theory]` du jeu de questions gold, aux noms très longs, échappaient au motif de comptage) —
toujours confirmer un total de suite de tests par une exécution réelle, jamais par une découverte
seule. La catégorie `Evaluation` (48 tests, jeu de questions gold) existe en plus de ces 245 mais
est volontairement exclue du compte "cœur" — voir `docs/CHECKLIST.md` milestone 9.

**8 Controllers** dans `src/Arhia.Api/Controllers/` : `AuthController` (register/login/me/
elevate-role), `EmployeeController`, `DepartmentController`, `WorkflowController` (instantiate/
check/close/archive), `TemplateController` (propose/verify/approve/reject), `ChatController` (SSE),
`NotificationController` (SSE), `AdminController` (`POST /api/admin/reindex-corpus`, QualityAdmin
uniquement).

**Pipeline RAG complet** (`Arhia.Infrastructure/Rag/`) : `MarkdownChunker` (structurel + recouvrement),
`XlmRobertaTokenizer` (correction d'offset SentencePiece→Hugging Face), `OnnxEmbeddingAdapter`
(768d, multilingue), `QdrantVectorSearchAdapter` (collection `agirh-corpus`), `OnnxRerankerAdapter`
(cross-encodeur `BAAI/bge-reranker-v2-m3`). Corpus réel : 6 documents → 72 chunks indexés.

**Orchestration conversationnelle** (`Arhia.Infrastructure/Llm/` + `Arhia.Core/UseCases/
AnswerConversationUseCase.cs`) : Router + Generator (Ollama, `phi4-mini:3.8b` pour les deux rôles),
garde-fou anti-hallucination en code (deux portes de sortie anticipée avant tout appel Generator).
**Limite mesurée et assumée** : ~27% de mauvais classement du routeur (13/48 sur le jeu de test) —
piste testée et abandonnée (few-shot doublé, température basse), voir `docs/HISTORIQUE.md`/
`NEXT_SESSION.md` pour le détail empirique. Ne pas re-tenter les mêmes pistes sans nouvelle mesure.

**Frontend complet** (`frontend/`, Next.js 15, pattern BFF) : pages publiques (accueil, login,
register), layout authentifié partagé (`app/(app)/`) avec tableau de bord par rôle, pages Dossiers
(liste + détail + actions check/close/archive) et Collaborateurs (liste + détail + création), page
Assistant (`/chat`, non migrée dans le layout `(app)/` — décision explicite). Mode clair/sombre
(`next-themes`), palette retravaillée avec contraste WCAG AA vérifié par script. Carte
Administration sur le tableau de bord (déclenche la réindexation RAG), visible QualityAdmin
uniquement.

**Sécurité** : JWT + ASP.NET Identity, `RbacMatrix` (3 rôles : `Employee`/`HR`/`QualityAdmin`),
`DepartmentScopeGuard` (portée département, vérifiée après RBAC — **le nom exact est
`DepartmentScopeGuard`, pas `PoleScopeGuard`**, renommé lors de la migration vocabulaire anglais).
Identité dérivée des claims JWT dans `src/Arhia.Api/Auth/CurrentUserAccessor.cs` — relit le
`UserAccount` en base à chaque requête (un compte désactivé ou dont le rôle a changé est pris en
compte immédiatement, sans réémission de token).

**Toujours pas fait** : logging technique/audit trail séparé (`IAuditTrailPort` documenté dans
`ARCHITECTURE.md` mais jamais implémenté — vérifié le 2026-08-30, aucun dossier
`Arhia.Infrastructure/Logging/` n'existe) ; les 3 cas particuliers métier (§8 `LOGIQUE_METIER.md`) ;
Phases 5-7 du frontend (modèles de checklist, administration complémentaire, finitions chat/
notifications) ; bascule vers un serveur Ollama d'entreprise (modèles plus capables, plan préparé
mais jamais confirmé en conditions réelles à ce jour). Vérifier avec `Glob` avant de citer un
chemin, ce projet évolue vite.

**Piège EF Core rencontré et corrigé** (à ne pas réintroduire) : une navigation de collection
owned (`OwnsMany`, ex. `WorkflowTemplate.Sections`, `TemplateSection.Items`,
`WorkflowInstance.Items`) **ne peut jamais être un paramètre de constructeur** — EF le rejette
explicitement ("Navigations to related entities, including references to owned types, cannot be
bound"). Ces 3 entités ont donc un **second constructeur privé** (scalaires uniquement, sans la
collection) dédié à la matérialisation EF, en plus du constructeur public riche utilisé par le code
applicatif. Par ailleurs, un paramètre de constructeur collection doit avoir EXACTEMENT le même
type que la propriété (`IReadOnlyCollection<T>`, pas `IEnumerable<T>`) pour que le binding par nom
fonctionne sur les propriétés scalaires converties (ex. `TemplateItem.ApplicableContractTypes`).

Ces documents sont **vivants** : à chaque décision produit/technique qui change, ils sont mis à
jour. Un agent qui trouve une contradiction entre son propre prompt et l'un de ces documents doit
**faire confiance au document**, pas à sa propre instruction figée, et le signaler plutôt que
trancher seul.

## Décisions structurantes déjà actées (résumé — détail dans docs/LOGIQUE_METIER.md)
- Périmètre : Onboarding/Offboarding uniquement (pas congés/CET/paie — dérive V7 corrigée)
- 3 rôles : Employee, RH (1 par pôle/département réel, ~5 pôles de 3-5 collaborateurs, tag "spécialisé IT" possible), Admin/Qualité (2 comptes, élèvent les rôles, jouent Vérificateur+Approbateur)
- Circuit qualité : RH = Rédacteur, Admin/Qualité = Vérificateur + Approbateur, templates de checklist versionnés (Draft→InReview→Approved/Rejected)
- **.NET 8** en pratique (seul SDK installé — voir docs/STACK_TECHNIQUE.md §1, .NET 10 visé à l'origine mais pas encore réévalué)
- Auth : JWT + ASP.NET Identity (Keycloak V7 abandonné)
- Données : SQL Server (relationnel) + Qdrant (vectoriel), tous deux en Docker
- Pipeline conversationnel : Router (intention) → Generator, agent **informatif uniquement** (aucune action destructrice/irréversible déclenchée depuis le chat, RBAC respecté par l'agent)
- Pipeline RAG à 4 phases **toutes obligatoires** : chunking structurel Markdown avec recouvrement (Microsoft.ML.Tokenizers) → embedding ONNX multilingue 768d (paraphrase-multilingual-mpnet-base-v2) → storage Qdrant (ANN/HNSW/cosine) → reranking cross-encoder ONNX (BAAI/bge-reranker-v2-m3)
- LLM génération (Router + Generator, distinct du pipeline RAG ci-dessus) : Ollama local, `phi4-mini:3.8b` pour les deux rôles (écart assumé — voir `docs/STACK_TECHNIQUE.md` §5)
- Temps réel : SSE (chat + barre de notifications par pôle/domaine)
- Logs : technique (debug/erreurs) + audit trail (qui a coché/validé quoi, quand) séparés — **décidé, jamais implémenté**
- Déploiement démo : Docker Compose 100% local (4 services + Ollama natif hors conteneur)
- Tests : xUnit + Moq + FluentAssertions, qa-executioner mobilisé systématiquement
- Renommage produit : le produit s'appelle **arhia** (toujours minuscules) — **AGIRH** reste le nom de l'entreprise d'accueil réelle, jamais celui du produit depuis le 2026-08-29/30

## Ancien pipeline V7 (Profiler → PreFlightValidator → ZeroTrustDispatcher → WorkerExecutor → Synthesizer → Checker) — SUPPRIMÉ
Ne plus le référencer comme architecture cible. Certains principes qu'il appliquait (anti-hallucination, fail-closed RBAC, validation stricte de toute sortie LLM avant usage) restent valides et ont été repris dans les agents mis à jour — mais la mécanique à 6 étapes elle-même est abandonnée : bug `MaxReflectionLoops` jamais corrigé en prod, RAG mal fondé, dérive de périmètre (cf. `docs/HISTORIQUE.md`).

## Cas particuliers métier encore ouverts (proposition à valider, docs/LOGIQUE_METIER.md §8)
Mutation inter-pôle, annulation/suspension de workflow, pôle vacant sans RH — comportements système proposés mais pas confirmés par le porteur du projet. Ne pas les considérer comme acquis dans une revue de code stricte.
