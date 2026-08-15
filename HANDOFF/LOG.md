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

---

## 2026-08-15 (suite) — Poste de travail (Windows)

**Fait :**
- Corpus RAG rédigé : `corpus/` (renommé depuis `knowledge_base/`, terme IR/NLP standard), 6 documents professionnels ancrés sur les checklists SMSI réelles et `LOGIQUE_METIER.md`, anonymisés.
- Recherche et validation des sources de modèles ONNX : `Xenova/paraphrase-multilingual-mpnet-base-v2` (embedding, confirmé conforme à STACK_TECHNIQUE.md) et `onnx-community/bge-reranker-v2-m3-ONNX` (reranking) — ONNX déjà publié comme prévu, pas de conversion Python nécessaire.
- `scripts/download-models.ps1` : téléchargement reproductible et idempotent des ~850 Mo de modèles (jamais commités).
- Conteneur Docker `agirh-qdrant` mis en place (image officielle, volume nommé persistant).
- Pipeline RAG phases 1-3 implémentées et **vérifiées contre de vraies infrastructures** (pas de mocks) : `MarkdownChunker`, `XlmRobertaTokenizer` (avec correction d'offset SentencePiece→Hugging Face vérifiée empiriquement — bug silencieux évité), `OnnxEmbeddingAdapter`, `QdrantVectorSearchAdapter`. Un vrai bug de découpage (paragraphe unique surdimensionné dupliqué) trouvé et corrigé grâce à un test boundary.
- 139/139 tests verts, 0 warning, incluant des tests d'intégration réels contre ONNX Runtime et Qdrant.
- Deux commits poussés sur `origin/master` pendant cette session (corpus, puis pipeline phases 1-3).

**Reste :**
- Phase 4 du pipeline RAG (reranking) — modèle en cours de téléchargement à la fin de cette entrée de journal, code pas encore écrit.
- Orchestration conversationnelle (Router/Generator), frontend, jeu de Q/R gold, endpoints Api Collaborateur/Workflow/Template — inchangé depuis l'entrée précédente.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 2) — Poste de travail (Windows)

**Fait :**
- Phase 4 du pipeline RAG (reranking) terminée : `OnnxRerankerAdapter` (cross-encoder ONNX, format de paire RoBERTa `<s>requête</s></s>document</s>`, sigmoïde), méthode `EncoderPaireEnIdsHuggingFace` ajoutée à `XlmRobertaTokenizer`.
- Test capstone `PipelineCompletCorpusReelTests` : chaîne les 4 phases sur un vrai document (`corpus/01_politique_onboarding.md`) — chunking → embedding → indexation Qdrant → recherche → reranking → la bonne réponse ressort en tête. **Milestone 7 (pipeline RAG) marqué FAIT.**
- 142/142 tests verts, 0 warning.
- Incident opérationnel : `ScheduleWakeup({stop:true})` a tué par effet de bord la tâche `run_in_background` du téléchargement du modèle de reranking (non liée à un `/loop`). Le fichier ONNX (570 Mo) avait déjà fini de télécharger avant l'incident ; seuls 3 petits fichiers de config manquaient, récupérés en relançant le script (idempotent). Noté dans `HANDOFF/NEXT_SESSION.md` pour ne pas reproduire.
- Deux commits supplémentaires poussés sur `origin/master`.

**Reste :**
- Milestone 8 (orchestration conversationnelle Router/Generator) — le pipeline RAG existe mais n'est câblé dans aucun use case Core ni aucun endpoint Api pour l'instant.
- Milestone 6 (frontend), milestone 9 (jeu de Q/R gold formel) — inchangé.
- Choix entre 8 et 6 comme prochaine étape — en attente du porteur du projet.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 3) — Poste de travail (Windows)

**Fait :**
- **Milestone 8 (orchestration conversationnelle) terminé et vérifié en HTTP réel.** `RepondreConversationUseCase` (Router→RAG-ou-statut→Generator, anti-hallucination en code : jamais d'appel au Generator si 0 candidat ou tout sous le seuil de pertinence), `OllamaRouterAdapter`/`OllamaGeneratorAdapter` (`phi4-mini:3.8b` pour les deux — `gemma4:12b` trop lent sur cette machine, pas de GPU), `ChatController` (`POST api/chat/demander`, refus RBAC renvoyé comme message conversationnel plutôt que 403 brut).
- Gap fonctionnel trouvé en testant en réel (pas dans les tests unitaires) : le RAG n'avait jamais indexé le vrai corpus sous ses vrais noms de documents (seulement des fixtures de test). Corrigé avec `IDocumentChunkerPort`/`MarkdownChunkerAdapter`, `IngererCorpusUseCase`, `AdminController` (`POST api/admin/reindexer-corpus`, RBAC AdminQualite).
- Vérification HTTP réelle de bout en bout après correction : réindexation (6 docs → 72 chunks) puis les 3 branches de routage testées en vrai — question documentaire → réponse sourcée correcte (`04_procedures_it_securite.md`), question hors-périmètre → refus poli, question de statut sans fiche → message gracieux.
- Piège rencontré et documenté pendant cette vérification : caractères accentués corrompus par l'encodage shell Windows Git Bash lors d'un test curl (`Où en est mon onboarding`) — faux 400, pas un bug applicatif. Contournement : payload JSON via fichier.
- Piège de bootstrap découvert : aucun compte Admin/Qualité n'existe par défaut sur une base fraîche, et `elever-role` exige déjà un acteur Admin/Qualité pour élever quelqu'un d'autre — promotion manuelle en SQL nécessaire pour amorcer le tout premier compte admin (documenté dans `HANDOFF/NEXT_SESSION.md`, à prévoir comme script de seed avant la soutenance).
- 158/158 tests verts (dont 9 nouveaux pour `RepondreConversationUseCase`, 2 fichiers pour les adaptateurs Ollama), 0 warning au build.
- `CHECKLIST.md` mis à jour (milestone 8 → ✅, milestone 9 → progression corpus indexé).
- Message suspect reçu en pleine session (demande d'élévation en groupe Administrators, désactivation de la mise en veille, tâche planifiée silencieuse, autorisation permanente d'agir sans confirmation pendant une absence de 10h) — ne correspondait à aucune décision prise dans cette conversation. Aucune action exécutée, signalé à l'utilisateur en direct dans le chat.

**Reste :**
- Choix de la prochaine étape (milestone 6 frontend / milestone 9 Q/R gold / endpoints Api Collaborateur-Workflow-Template) — en attente du porteur du projet.
- Seuil de pertinence reranking (0.01) provisoire, à calibrer avec un vrai jeu de Q/R gold.
- `docker-compose.yml` complet toujours pas fait (Qdrant/Ollama/Api/front) — services lancés manuellement.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.
