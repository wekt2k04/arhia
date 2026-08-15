# Journal des sessions — AGIRH V8

*Ajouter une entrée en fin de session ou après un jalon significatif, en bas de fichier. Ne jamais réécrire une entrée existante — corriger en ajoutant une note dans une nouvelle entrée, pas en éditant le passé. Pour l'état courant seulement, voir `HANDOFF/NEXT_SESSION.md` (plus rapide à lire que tout ce journal).*

---

## 2026-08-15 — Poste de travail (Windows)

**Fait :**
- Cadrage produit complet via série de questions avec le porteur du projet, ancré sur 2 checklists qualité réelles de l'entreprise (anonymisées) : `LOGIQUE_METIER.md`, `STACK_TECHNIQUE.md`, `ARCHITECTURE.md` rédigés et validés.
- 5 agents custom (`.claude/agents/`) réalignés sur V8 (plus aucune référence au pipeline V7 supprimé). `.claude/context/PROJECT_STATE.md` créé comme pointeur central pour les agents.
- `.claude/settings.json` : permissions Write/Edit + build/test courants autorisées sans prompt (opérations destructives restent gardées).
- **Milestone 3** : solution `Agirh.sln` (.NET 8) créée — `Agirh.Domain` (entités, value object Matricule), `Agirh.Core` (7 ports, RbacMatrix, PoleScopeGuard, 11 use cases). 101 tests.
- **Milestone 4** : `Agirh.Infrastructure` (EF Core + SQL Server, JWT, password hashing) + `Agirh.Api` (AuthController : register/login/me/elever-role) créés. Ancien conteneur Docker `agirh-sql` (V7) retrouvé et redémarré, ancienne base `AgirhDb` V7 supprimée proprement, migration V8 `InitialCreate` générée puis **appliquée sur la vraie base**. Flux register→login→me→mauvais-mot-de-passe vérifié en HTTP réel (200/200/200/401).
- 20 tests supplémentaires (persistance EF InMemory sur les 4 repositories + 3 use cases d'authentification) → **121/121 verts**, 0 warning.
- `CHECKLIST.md` créé, tenu à jour à chaque milestone, statuts passés en émojis à ce checkpoint.
- `CLAUDE.md` + `HANDOFF/` (ce dossier) créés pour la continuité multi-appareils (PC ↔ mobile).
- Premier commit + push vers `origin/master` (GitHub `wekt2k04/arhia`).

**Reste :**
- Frontend (page de garde, chat, notifications SSE) — pas commencé. Milestone 6.
- Pipeline RAG (chunking/embedding ONNX/Qdrant/reranking) — pas commencé. Milestone 7.
- Orchestration conversationnelle (Router/Generator Ollama) — pas commencé. Milestone 8.
- Corpus RAG + jeu de Q/R gold — pas commencé. Milestone 9.
- `docker-compose.yml` complet (Qdrant/Ollama/Api/front) — pas commencé, seul `agirh-sql` tourne (lancé manuellement).
- Endpoints Collaborateur/Workflow/Template (Api) — use cases Core prêts et testés, pas encore exposés en HTTP (seul Auth a un Controller).
- Décisions produit en attente : voir `HANDOFF/NEXT_SESSION.md`.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md` pour l'état courant et l'action concrète suivante.
