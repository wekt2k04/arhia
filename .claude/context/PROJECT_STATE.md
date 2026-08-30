# État du projet — arhia V8

Le projet a été **entièrement remis à zéro** le 2026-08-14 (V7 → V8, voir `docs/HISTORIQUE.md` à la racine). Le code V7 est supprimé ; les agents custom (`.claude/agents/`) ont été réalignés sur les décisions V8, mais **toute règle qui cite un fichier/chemin V7** doit être traitée comme obsolète tant que ce fichier n'existe pas réellement — vérifier avec `Glob`/`Grep` avant de s'appuyer dessus.

## Specs canoniques (sous `docs/` à la racine du repo)
1. **`docs/LOGIQUE_METIER.md`** — rôles, workflow onboarding/offboarding, RBAC, garde-fous IA. **Fait.**
2. **`docs/STACK_TECHNIQUE.md`** — stack backend/frontend/données/IA. **Fait.**
3. **`docs/ARCHITECTURE.md`** — hexagonal, arborescence de dossiers, diagrammes. **Fait.**
4. **`docs/CHECKLIST.md`** — suivi milestone par milestone (statuts en émojis, livré, reste à faire). Mis à jour à la fin de chaque étape.
5. **`.claude/HANDOFF/NEXT_SESSION.md`** + **`.claude/HANDOFF/LOG.md`** — protocole de continuité multi-appareils (PC ↔ mobile), décrit dans `CLAUDE.md` à la racine. Une session fraîche (y compris ces agents custom) doit s'y référer pour l'état courant plutôt que de le redécouvrir.

## Code existant (milestones 3-4, docs/CHECKLIST.md)
`Arhia.sln` (.NET 8, pas 10 — SDK réellement installé) : `Arhia.Domain`, `Arhia.Core`, `Arhia.Infrastructure` (EF Core + SQL Server, JWT, password hashing), `Arhia.Api` (ASP.NET Core, AuthController), `Arhia.Tests`. **121/121 tests verts**, 0 warning. Migration `InitialCreate` appliquée sur le conteneur Docker réel `arhia-sql` (port 1433, remis à zéro pour V8) ; flux register/login/me vérifié en HTTP réel.

**Toujours pas fait** : endpoints Collaborateur/Workflow/Template (seul Auth a un Controller), pipeline RAG, orchestration conversationnelle, frontend, `docker-compose.yml` (Qdrant/Ollama pas encore ajoutés). Ne pas supposer qu'un contrôleur au-delà d'Auth existe sans vérifier avec `Glob`.

**Piège EF Core rencontré et corrigé** (à ne pas réintroduire) : une navigation de collection owned (`OwnsMany`, ex. `WorkflowTemplate.Sections`, `TemplateSection.Items`, `WorkflowInstance.Items`) **ne peut jamais être un paramètre de constructeur** — EF le rejette explicitement ("Navigations to related entities, including references to owned types, cannot be bound"). Ces 3 entités ont donc un **second constructeur privé** (scalaires uniquement, sans la collection) dédié à la matérialisation EF, en plus du constructeur public riche utilisé par le code applicatif. Par ailleurs, un paramètre de constructeur collection doit avoir EXACTEMENT le même type que la propriété (`IReadOnlyCollection<T>`, pas `IEnumerable<T>`) pour que le binding par nom fonctionne sur les propriétés scalaires converties (ex. `TemplateItem.ConditionsTypeContrat`).

Ces documents sont **vivants** : à chaque décision produit/technique qui change, ils sont mis à jour. Un agent qui trouve une contradiction entre son propre prompt et l'un de ces documents doit **faire confiance au document**, pas à sa propre instruction figée, et le signaler plutôt que trancher seul.

## Décisions structurantes déjà actées (résumé — détail dans docs/LOGIQUE_METIER.md)
- Périmètre : Onboarding/Offboarding uniquement (pas congés/CET/paie — dérive V7 corrigée)
- 3 rôles : Collaborateur, RH (1 par pôle/département réel, ~5 pôles de 3-5 collaborateurs, tag "spécialisé IT" possible), Admin/Qualité (2 comptes, élèvent les rôles, jouent Vérificateur+Approbateur)
- Circuit qualité : RH = Rédacteur, Admin/Qualité = Vérificateur + Approbateur, templates de checklist versionnés (T0→T1)
- **.NET 8** en pratique (seul SDK installé — voir docs/STACK_TECHNIQUE.md §1, .NET 10 visé à l'origine mais pas encore réévalué)
- Auth : JWT + ASP.NET Identity (Keycloak V7 abandonné)
- Données : SQL Server (relationnel) + Qdrant (vectoriel), tous deux en Docker
- Pipeline agentique : Router (intention) → Generator, agent **informatif uniquement** (aucune action destructrice/irréversible déclenchée depuis le chat, RBAC respecté par l'agent)
- Pipeline RAG à 4 phases **toutes obligatoires** : chunking structurel Markdown avec recouvrement (Microsoft.ML.Tokenizers) → embedding ONNX multilingue 768d (ex. paraphrase-multilingual-mpnet-base-v2) → storage Qdrant (ANN/HNSW/cosine) → reranking cross-encoder ONNX (BAAI/bge-reranker-v2-m3)
- LLM génération (Router + Generator, distinct du pipeline RAG ci-dessus) : Ollama local, 2 modèles (un petit/rapide pour le routeur, un plus capable pour le générateur)
- Temps réel : SSE (chat + barre de notifications par pôle/domaine)
- Logs : technique (debug/erreurs) + audit trail (qui a coché/validé quoi, quand) séparés
- Déploiement démo : Docker Compose 100% local
- Tests : même rigueur que V7 (xUnit + Moq + FluentAssertions), qa-executioner mobilisé systématiquement

## Ancien pipeline V7 (Profiler → PreFlightValidator → ZeroTrustDispatcher → WorkerExecutor → Synthesizer → Checker) — SUPPRIMÉ
Ne plus le référencer comme architecture cible. Certains principes qu'il appliquait (anti-hallucination, fail-closed RBAC, validation stricte de toute sortie LLM avant usage) restent valides et ont été repris dans les agents mis à jour — mais la mécanique à 6 étapes elle-même est abandonnée : bug `MaxReflectionLoops` jamais corrigé en prod, RAG mal fondé, dérive de périmètre (cf. `docs/HISTORIQUE.md`).

## Cas particuliers métier encore ouverts (proposition à valider, docs/LOGIQUE_METIER.md §8)
Mutation inter-pôle, annulation/suspension de workflow, pôle vacant sans RH — comportements système proposés mais pas confirmés par le porteur du projet. Ne pas les considérer comme acquis dans une revue de code stricte.
