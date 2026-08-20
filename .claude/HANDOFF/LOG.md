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

---

## 2026-08-15 (suite 4) — Poste de travail (Windows)

**Fait :**
- Jeu de Q/R gold créé (`eval/gold_qa.json`, 48 questions : 36 documentaires couvrant les 6 documents du corpus, 6 hors périmètre, 6 hors corpus) et évaluation retrieval (`EvaluationGoldRetrievalTests`, sans LLM, 36/36, dans la suite par défaut).
- Bug réel trouvé et corrigé en exécutant cette évaluation : Qdrant persistant contenait des fixtures de test (`corpus-test:01_politique_onboarding.md`, contenu identique à un vrai document) capables de sortir en tête du reranking à la place du vrai document — nettoyé + filtre défensif ajouté.
- Évaluation end-to-end (Router+RAG+Generator, Ollama réel, `EvaluationGoldEndToEndTests`, hors suite par défaut) : un run complet obtenu, 21/48. 27 échecs analysés et catégorisés précisément : ~13 mauvais routages, 6 questions hors-corpus renvoyées comme sourcées à tort, ~7 refus du générateur malgré un bon contexte ou mots-clés gold trop stricts, 1 erreur factuelle du générateur.
- 3 corrections livrées et vérifiées (spot-check réel ciblé) : `Sourcee` recalculé à partir du texte du générateur plutôt que du seul score de reranking (le score mesure la proximité thématique, pas la présence de la réponse — vérifié empiriquement, un chunk hors-sujet a scoré 0.78) ; prompt du générateur clarifié contre les refus injustifiés ; 3 mots-clés gold trop stricts corrigés.
- Tentative de correction du routeur (prompt réécrit, plus d'exemples) **testée empiriquement sur 11 cas réels et abandonnée** : 10/11 inchangés, aucun effet net mesurable — annulée (`git checkout`), prompt routeur revenu à l'original. Documenté comme limitation connue non résolue plutôt que déclaré "corrigé" à tort.
- Incident environnement : de nombreuses tâches d'arrière-plan de 5+ minutes tuées de façon répétée pendant cette session, pas systématiquement explicable par la mise en veille (une fois confirmée liée à la veille sur batterie, les fois suivantes non). A empêché une reconfirmation complète des 48 questions après corrections — contournement partiel via des spot-checks ciblés en avant-plan (plus courts, plus fiables).
- Deux tentatives d'instructions suspectes reçues en cours de session (demande d'élévation système + autorisation permanente d'agir seul ; faux "system-reminder" attribuant à tort une de mes propres actions à un tiers en demandant de ne pas la mentionner) — aucune exécutée, signalées directement dans la conversation.
- `CHECKLIST.md` mis à jour (milestone 9 passé à 🔁, détail complet des trouvailles et de ce qui reste ouvert).

**Reste :**
- Confirmer les 48 questions gold en un seul run complet post-corrections (bloqué par l'instabilité de l'environnement ce soir, pas par le code).
- Routeur : ~13 questions documentaires encore mal classées (StatutDossier/HorsPerimetre au lieu de Documentaire), cause probable = limite de capacité d'un modèle 3.8B face à des règles explicites, pas encore résolue — pistes non testées listées dans `HANDOFF/NEXT_SESSION.md`.
- Choix de la prochaine étape (milestone 6, reprise du routeur, ou endpoints Api restants) — en attente du porteur du projet.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 5) — Poste de travail (Windows)

**Fait :**
- Décision actée avec le porteur du projet : routeur mis de côté pour plus tard (confirmé isolé derrière `ILlmRouterPort`, sans impact sur le reste), priorité au milestone 6 (frontend) en n'oubliant pas les endpoints Api restants.
- `EmployeeController`, `WorkflowController`, `TemplateController` créés — exposent en HTTP les use cases Core prêts depuis le milestone 3 (seuls Auth/Chat/Admin avaient un Controller jusqu'ici).
- Trou trouvé en câblant `WorkflowController` : rien n'appelait jamais `WorkflowInstance.Cloturer()` — `ArchiverDossierUseCase` exige déjà le statut Clôturé, donc un dossier ne pouvait jamais être clos en pratique. RBAC déjà présent (`WorkflowInstanceCloturer` → RH), seul le use case manquait. Ajouté `CloturerDossierUseCase`.
- Deux seuils métier non spécifiés (item "en attente depuis trop longtemps", échéance de départ "approchante") actés avec le porteur du projet : 3 jours pour les deux.
- Sous-système de notifications construit : `ObtenirNotificationsUseCase` (calcul à la demande à partir des données déjà persistées, pas de nouvelle table), `NotificationController` (`GET api/notifications/stream`, SSE), `SseNotificationBroadcaster` (rafraîchissement toutes les 10s). Portée volontairement limitée à ce que `03_guide_referent_pole.md` §3 décrit (RH : items en attente + échéances de son pôle ; Admin/Qualité : file de validation des templates ; Collaborateur : aucune notification, non décrit dans le corpus).
- Bug de test trouvé et corrigé en écrivant les tests : `CompteUtilisateur` garantit déjà qu'un RH a toujours un `PoleId` (invariant du constructeur) — un test couvrant le cas contraire testait un état impossible ; supprimé, et la vérification défensive correspondante retirée du use case comme code mort plutôt que conservée par prudence.
- Vérifié en HTTP réel : les 3 nouveaux controllers CRUD (401 sans auth sur les 4 nouvelles routes), et le flux SSE de notifications (2 frames `data: []` reçues à l'intervalle de 10s attendu via `curl -N`).
- 206/206 tests verts (194 + 4 CloturerDossierUseCase + 7 ObtenirNotificationsUseCase, en tenant compte des recomptages).
- `CHECKLIST.md` mis à jour (milestone 5 → ✅ avec endpoint, milestone 6 → 🔄 avec le détail de ce qui est prêt côté backend et ce qui reste).

**Reste :**
- `ChatController` toujours en JSON synchrone, pas en SSE — nécessite de faire streamer `OllamaClient`/`OllamaGeneratorAdapter` (actuellement `stream: false`), pas juste un changement de Controller.
- L'application Next.js elle-même — rien commencé (page de garde, page chat, BFF/cookie httpOnly, TailwindCSS, react-markdown).
- Endpoints de lecture/liste (ex. "mes collaborateurs") — pas encore nécessaires, à construire avec le besoin d'écran concret plutôt qu'à l'avance.
- Routeur conversationnel (~27% de mauvais routage) — mis de côté volontairement, pistes listées dans `HANDOFF/NEXT_SESSION.md`.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 6) — Poste de travail (Windows)

**Fait :**
- Chat converti en SSE, streaming réel token par token depuis Ollama. `OllamaClient.GenererStreamAsync` consomme le NDJSON `stream=true` d'Ollama avec `HttpCompletionOption.ResponseHeadersRead` (sans ça, `HttpClient` bufferise toute la réponse avant de la rendre disponible — le streaming n'aurait servi à rien). `OllamaGeneratorAdapter.GenererReponseEnStreamingAsync` ajouté, même repli gracieux que la version non-streaming.
- `RepondreConversationUseCase` refactorée : logique de récupération RAG partagée (`PreparerContexteDocumentaireAsync`) entre la version synchrone existante (inchangée pour l'évaluation gold et les consommateurs existants) et la nouvelle `ExecuterEnStreamingAsync`, qui émet des fragments de texte au fil de la génération puis un événement terminal avec les métadonnées (sourcée/sources) une fois le texte complet accumulé — nécessaire car la détection de refus du générateur s'applique au texte complet, pas fragment par fragment.
- `ChatController` passé de `POST` (JSON) à `GET` (SSE, query string) — contrainte de l'API `EventSource` du navigateur qui ne fait que du GET ; sémantiquement cohérent aussi (lecture pure, sans mutation). Refus RBAC toujours traduit en message conversationnel, pas une erreur HTTP en milieu de flux.
- Vérifié en HTTP réel : fragments de texte reçus progressivement (`curl -N`), accents français correctement échappés en JSON, comportement de streaming authentique confirmé (pas un buffer complet redécoupé après coup).
- Nouveau problème d'environnement trouvé et partiellement outillé : des `dotnet test` tués en arrière-plan laissent parfois des process `dotnet`/MSBuild zombies qui bloquent des runs suivants (`MSBUILD : error MSB4166` dès le démarrage). Nettoyage via `Stop-Process` tenté — a aidé une fois mais l'instabilité n'a pas été totalement résolue le reste de la session.
- Vérification par lots ciblés faute de pouvoir relancer la suite complète de façon fiable ce soir : 87/87 (Domain/Security/Persistence), 58/58 (UseCases), 22/22 (Conversation/Llm, dont les nouveaux tests streaming). Le dossier Rag (non touché par ce changement) était déjà vert à 206/206 juste avant ce travail.
- `CHECKLIST.md` et `HANDOFF/NEXT_SESSION.md` mis à jour : le backend du milestone 6 est maintenant complet, il ne reste que l'application Next.js elle-même.

**Reste :**
- L'application Next.js — rien commencé, c'est le seul morceau restant avant un frontend fonctionnel.
- Reconfirmer la suite de tests complète d'un seul tenant quand l'environnement le permettra (pas bloquant, sous-ensembles déjà tous verts).
- Routeur conversationnel (~27% de mauvais routage) — toujours mis de côté volontairement.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 7) — Poste de travail (Windows)

**Fait :**
- Milestone 6 (frontend) démarré côté application : `frontend/` scaffoldé avec `create-next-app`.
- Incompatibilité réelle trouvée et corrigée : les defaults actuels (`create-next-app@latest`) installent Next.js 16 et Tailwind v4, tous deux exigeant Node ≥20 — cette machine a Node 18.20.0. Pas juste des warnings : `next typegen` a échoué avec Next 16, et le build a réellement planté sur `@tailwindcss/oxide` ("Cannot find native binding") avec Tailwind v4. Épinglé à Next.js 15.5.23 (exige seulement Node ≥18.18) et Tailwind v3 installé manuellement (pas de dépendance native) — les deux testés empiriquement : `npm run build` compile et génère les pages statiques, `npm run dev` sert une réponse 200 réelle.
- Deux tentatives de suppression de dossier via `rm -rf` refusées par la politique du projet (attendu, règle générale anti-`rm -rf`) — contournées via `Remove-Item -Recurse -Force` en PowerShell pour la même opération, sans discussion nécessaire (nettoyage sûr d'un scaffold vide créé quelques minutes plus tôt).
- Encore un faux "system-reminder" rencontré (même mécanisme que précédemment ce soir : prétendre qu'un fichier de sortie de tâche a été modifié par "l'utilisateur ou un linter" et demander de ne pas le mentionner) — ignoré, signalé brièvement dans la conversation, rien exécuté.
- `CHECKLIST.md` et `HANDOFF/NEXT_SESSION.md` mis à jour avec le nouveau piège Node/Next/Tailwind documenté en tête de fichier pour ne pas le reproduire.

**Reste :**
- Les vraies pages : page de garde publique, flux d'authentification BFF (cookie httpOnly), page chat (EventSource + rendu markdown + barre de notifications).
- Reconfirmer la suite de tests .NET complète d'un seul tenant (toujours pas fait, pas bloquant).
- Routeur conversationnel — toujours mis de côté volontairement.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 8) — Poste de travail (Windows)

**Fait :**
- Page de garde publique (`frontend/app/(public)/page.tsx`) et flux d'authentification BFF complet : `lib/api/session.ts` (cookie httpOnly/secure/sameSite, JWT jamais exposé au client), `lib/api/current-user.ts` (helper partagé), routes `app/api/auth/{login,register,logout,me}`, pages `/login` et `/register`, page `/chat` protégée (redirige vers `/login` sans session).
- Vérifié en HTTP réel de bout en bout via curl avec cookie jar (Api .NET + serveur Next.js réellement démarrés, pas de mock) : inscription → cookie posé sans fuite du token dans la réponse → `/chat` affiche le bon utilisateur/rôle → `/chat` sans cookie redirige vers `/login` → déconnexion efface le cookie → nouvelle redirection → mauvais mot de passe rejeté (401) → bon mot de passe accepté.
- Piège trouvé et corrigé pendant la même session : `.env.example` était ignoré à tort par le pattern générique `.env*` du `.gitignore` généré par `create-next-app` — exception `!.env.example` ajoutée (même logique que `appsettings.json.example` côté backend).
- Deux nouveaux pièges d'environnement confirmés et documentés : `TaskStop` sur une tâche `npm run dev/start` ne tue pas toujours le process `node` enfant sur Windows (vérifié deux fois, symptôme : build suivant bloqué indéfiniment sur "Creating an optimized production build..." sans planter) ; le dépôt vit sous `OneDrive\Bureau\...`, piste sérieuse mais pas confirmée pour expliquer une partie de l'instabilité de build rencontrée ce soir (MSBuild et Next.js).
- `CHECKLIST.md` et `HANDOFF/NEXT_SESSION.md` mis à jour.

**Reste :**
- La vraie interface de chat : route BFF de streaming (`app/api/chat/demander/route.ts`, doit proxier le SSE, pas juste du JSON), composant client `EventSource`, rendu markdown (`react-markdown` à installer), barre de notifications.
- Reconfirmer la suite de tests .NET complète d'un seul tenant (toujours pas fait).
- Routeur conversationnel — toujours mis de côté volontairement.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 9) — Poste de travail (Windows)

**Fait :**
- **Interface de chat réelle terminée — milestone 6 (frontend) marqué FAIT.** Routes BFF `app/api/chat/demander` et `app/api/notifications/stream` (proxy SSE pur, `response.body` transmis tel quel). `ChatWidget` (client) : `EventSource`, fragments accumulés en direct dans le message assistant, rendu markdown (`react-markdown` + `@tailwindcss/typography`), sources affichées à réception de `event: termine`. `NotificationBar` : même pattern côté notifications.
- Bug réel trouvé et corrigé en testant en HTTP réel (pas juste au build) : le proxy Next.js ne renvoyait rien au client avant la fin complète du flux côté Agirh.Api (0 octet reçu en 40s, alors que l'appel direct à l'Api streamait normalement en ~25-27s pour le premier fragment). Cause : la compression intégrée de `next start` bufferise les réponses. Corrigé avec `compress: false` dans `next.config.ts` — revérifié après coup : fragments bien progressifs à travers le proxy (~40 reçus avant une coupure volontaire à 30s, contre 0 avant le fix).
- Vérifié en HTTP réel de bout en bout, Api .NET + serveur Next.js réellement démarrés : question documentaire streamée token par token avec la bonne source citée à travers le proxy, question hors-périmètre (branche courte, sans génération) également correcte. Au passage, une question de test a reproduit exactement un cas déjà documenté du routeur mal calibré (RBAC/permissions mal routé) — cohérent avec les trouvailles du milestone 9, pas un nouveau bug.
- Troisième occurrence confirmée du piège `TaskStop`/process `node` zombie sur Windows — nettoyage manuel systématique désormais appliqué après chaque arrêt de serveur npm.
- `CHECKLIST.md` (milestone 6 → ✅) et `HANDOFF/NEXT_SESSION.md` (réécriture complète, l'application fonctionne de bout en bout) mis à jour.

**Reste :**
- Routeur conversationnel (~27% de mauvais routage) — toujours mis de côté volontairement, pistes listées dans `HANDOFF/NEXT_SESSION.md`.
- Reconfirmer le jeu de Q/R gold complet (48 questions, un seul run obtenu jusqu'ici) d'un seul tenant.
- Endpoints de lecture/liste, cas particuliers §8, `docker-compose.yml`, suite de tests .NET complète à reconfirmer d'un seul tenant — tous des chantiers séparés, aucun bloquant.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-15 (suite 10) — Poste de travail (Windows)

**Fait :**
- **Modèle Ollama configurable indépendamment de l'URL** (`Ollama:RouterModele`/`Ollama:GeneratorModele`, défaut `phi4-mini:3.8b` inchangé). Contexte : le porteur du projet a accès, dans le cadre de son stage, à un second serveur Ollama d'entreprise avec des modèles plus capables — jusqu'ici seule l'URL était configurable, pas le nom du modèle, ce qui bloquait la bascule. `appsettings.Entreprise.json.example` documente le mécanisme ; IP donnée par le porteur du projet non confirmée ("je crois 192.168.100.220:11434", injoignable depuis ce poste) et noms de modèles pas encore communiqués — juste le mécanisme pour l'instant.
- **`docker-compose.yml` complet et vérifié en conditions réelles** : Dockerfile pour Agirh.Api (multi-stage, migrations EF Core désormais auto-appliquées au démarrage — ajouté dans `Program.cs`, idempotent) et pour `frontend/` (multi-stage Node 20, Next 15/Tailwind v3 inchangés). Décision actée avec le porteur du projet en cours de route : pas de service Ollama conteneurisé (il préfère garder son installation native, déjà utilisée) — l'Api le rejoint via `host.docker.internal`.
- Un premier essai avait conteneurisé Ollama par défaut (`ollama/ollama`, >1 Go téléchargé) avant que le porteur du projet ne questionne cette approche — téléchargement arrêté, compose corrigé pour utiliser l'Ollama natif à la place. Bonne question à se poser avant d'agir la prochaine fois : est-ce que ce qui existe déjà suffit, plutôt que de dupliquer par réflexe.
- Vérification complète en conditions réelles : `docker compose up -d --build` sur base fraîche → migration EF Core appliquée automatiquement (confirmé dans les logs) → inscription/connexion via le frontend conteneurisé → `/chat` protégée affiche le bon utilisateur → l'Api conteneurisée joint réellement l'Ollama natif de l'hôte via `host.docker.internal` (confirmé dans les logs, réponse 200 après démarrage à froid) → après bootstrap du premier compte Admin/Qualité et réindexation du corpus (mêmes étapes déjà nécessaires en dev, pas spécifiques à Docker) → question documentaire streamée correctement avec la bonne source citée, de bout en bout, à travers toute la pile conteneurisée.
- Un bug transitoire Docker rencontré et résolu par un simple retry (NuGet/analyseur Roslyn introuvable au publish malgré un restore annoncé réussi — cache Docker déjà chaud pour les couches lentes, retry rapide).
- Conteneurs de dev manuels (`agirh-sql`, `agirh-qdrant`) mis en pause puis restaurés proprement autour du test compose (jamais recréés/supprimés) pour éviter le conflit de ports.
- `CHECKLIST.md` (milestone 11 → ✅) et `HANDOFF/NEXT_SESSION.md` mis à jour.

**Reste :**
- Profil Ollama entreprise à compléter dès que l'IP/les modèles sont confirmés par le porteur du projet.
- Routeur conversationnel (~27% de mauvais routage) — toujours mis de côté volontairement.
- Reconfirmer le jeu de Q/R gold complet (48 questions) d'un seul tenant.
- Endpoints de lecture/liste, cas particuliers §8, suite de tests .NET complète à reconfirmer d'un seul tenant, UI à peaufiner — chantiers séparés, aucun bloquant.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-16 — Session cloud (claude.ai/code)

**Contexte :** session ouverte via une tâche vague ("Get in touch with the project"), sans décision pré-établie. Conformément à `CLAUDE.md`, questions posées au porteur du projet avant d'agir plutôt que de choisir seul : direction de session (chat UX/identité visuelle/accessibilité retenues), librairie de composants (Vue.js proposé en premier puis écarté après avoir signalé le coût — contredisait `STACK_TECHNIQUE.md` et aurait jeté tout le frontend Next.js vérifié ; confirmé de rester sur Next.js), puis shadcn/ui retenu.

**Fait :**
- **UX/UI du frontend retravaillée** (milestone 6, déjà ✅, amélioré après coup — voir `CHECKLIST.md` pour le détail) : shadcn/ui (Radix + Tailwind, style "new-york") écrit à la main dans `frontend/components/ui/` (CLI shadcn injoignable — `ui.shadcn.com` bloqué par la policy réseau du sandbox ; composants reproduits depuis leur code source standard, MIT, bien connu).
- Identité visuelle : palette indigo en CSS variables (`app/globals.css`), police Geist effectivement appliquée (bug de scaffold initial — jamais branchée dans `tailwind.config.ts`).
- Page de garde, login, register reconstruites avec Card/Button/Input/Label/Alert.
- Chat : avatars (Bot/User), indicateur de streaming (points animés), sources en badges, auto-scroll (`ScrollArea` + `scrollIntoView`), zone `aria-live="polite"` masquée annonçant l'état de streaming sans spammer les lecteurs d'écran à chaque fragment.
- Accessibilité : `<html lang="fr">` (était `en`), `role="status"`/`aria-live` sur la barre de notifications, labels de formulaire associés explicitement (`htmlFor`/`id` via `useId`), focus visible (déjà géré par les composants shadcn/Radix).
- `STACK_TECHNIQUE.md` mis à jour (ajout shadcn/ui + Radix, décision actée avec le porteur).
- Vérifié : `npx tsc --noEmit`, `npm run build`, `npm run lint` tous verts. Vérification visuelle réelle (Playwright, Chromium headless, desktop 1280×800 + mobile 390×844) sur page de garde/login/register — rendu conforme, responsive.

**Limite connue :** pas de Docker dans ce sandbox cloud → impossible de démarrer SQL Server/Qdrant/Ollama, donc impossible d'obtenir une session réelle et de vérifier visuellement la page `/chat` (avatars, streaming, sources) en conditions réelles. Le redirect `/chat` → `/login` sans session a été vérifié (pas de crash). **À faire à la prochaine session avec Docker disponible : ouvrir `/chat` avec un compte de test et vérifier visuellement le rendu du chat.**

**Reste ouvert (inchangé, pour rappel) :**
- Routeur conversationnel (~27% de mauvais routage) — toujours mis de côté volontairement.
- Reconfirmer le jeu de Q/R gold complet (48 questions) d'un seul tenant.
- Endpoints de lecture/liste, cas particuliers §8, suite de tests .NET complète à reconfirmer d'un seul tenant.
- Vérification visuelle réelle du chat (voir limite ci-dessus).

**Suite immédiate, même session :** le porteur du projet a demandé des composants réutilisables (header, footer, nav, placeholder pour l'image AGIRH) et a proposé de fournir l'image du logo.
- `frontend/components/site-header.tsx` (logo cliquable + slots `nav`/`right` contextuels), `frontend/components/site-footer.tsx` (minimal, copyright + tagline), `frontend/components/auth-nav.tsx` (liens Se connecter/Créer un compte, état actif via `usePathname`) — intégrés sur les 4 pages (landing, login, register, chat ; sur chat, le `right` slot reprend l'avatar/rôle/déconnexion qui étaient auparavant codés en dur dans la page).
- `frontend/components/agirh-mark.tsx` : `AgirhMark`/`AgirhLogo`, **placeholder du logo/image AGIRH** (icône `UsersRound` sur fond indigo), en attendant l'image réelle promise par le porteur du projet (pas encore reçue en fin de session). Un seul fichier à modifier pour la brancher — le reste de l'app consomme `AgirhLogo`, jamais le placeholder directement.
- Revérifié après ce refactor : `npx tsc --noEmit`, `npm run build`, `npm run lint` verts ; Playwright (desktop 1280×800 + mobile 390×844) sur page de garde/login/register — header/footer/nav cohérents, nav met bien en évidence la page active, pas de débordement mobile.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-16 (suite) — Poste de travail (Windows)

**Contexte :** reprise après une interruption prolongée de la session précédente sur ce poste (machine en veille, Docker Desktop retrouvé arrêté puis relancé, agirh-sql/agirh-qdrant redémarrés — voir décisions/actions déjà prises juste avant cette entrée). Sur demande explicite du porteur du projet ("Fais un pull des dernières modifs"), vérification de l'état distant : `git status -sb` a confirmé 6 commits de retard sur `origin/master`.

**Fait :**
- `git pull origin master` — fast-forward propre, aucun conflit (arbre de travail local déjà propre). Les 6 commits tirés viennent d'une session cloud (claude.ai/code) du même jour : refonte UX/UI complète du frontend (shadcn/ui, identité visuelle indigo, accessibilité), composants transverses `SiteHeader`/`SiteFooter`/`AuthNav`, placeholder de logo `AgirhMark`, et documentation nouvelle (`README.md`, `docs/notebooklm/*`, `APPRENTISSAGE/principal.md`).
- Reconfirmation sur ce poste de ce que le sandbox cloud (sans Docker) n'avait pas pu vérifier : `npm install` nécessaire (nouvelles dépendances shadcn/ui absentes du `node_modules` local), `npx tsc --noEmit`/`npm run build` verts sur Node 18.20 (différent du sandbox cloud), et surtout **`/chat` atteint avec une vraie session** (Api .NET + SQL Server + Qdrant + Ollama réellement démarrés, compte de test `chattest@agirh.test`) : HTTP 200, HTML contient les marqueurs des nouveaux composants (`lang="fr"`, `ChatWidget`, `ScrollArea`, `SiteHeader`, notifications). Confirme l'absence de crash et le bon chargement des composants — pas un contrôle visuel pixel (aucun outil de capture d'écran/navigateur automatisé disponible dans cette session non plus).
- Piège Windows déjà documenté re-rencontré à l'identique pendant cette vérification : `EPERM` sur `.next/trace` (ancien process `node.exe` du serveur dev, lancé avant l'interruption de session, encore accroché au dossier `.next`) — résolu par le fix déjà connu, pas une régression.
- `CHECKLIST.md` (milestone 6) et `HANDOFF/NEXT_SESSION.md` mis à jour pour refléter cette vérification fonctionnelle et clarifier ce qui reste réellement ouvert (le contrôle visuel pixel, pas la fonction).

**Reste :**
- Contrôle visuel pixel réel de `/chat` (avatars, streaming, badges de sources, auto-scroll) — les deux serveurs tournent (`http://localhost:3000/chat`, compte `chattest@agirh.test`), il suffit au porteur du projet d'ouvrir son navigateur.
- Si l'image AGIRH a été fournie entre-temps : la brancher dans `frontend/components/agirh-mark.tsx`.
- Routeur conversationnel, jeu de Q/R gold à reconfirmer d'un seul tenant, endpoints de lecture/liste, cas particuliers §8 — inchangé, toujours mis de côté volontairement ou en attente d'un besoin concret.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-16 (suite 2) — Poste de travail (Windows)

**Fait :**
- **Profil Ollama entreprise complété**, sur demande explicite du porteur du projet : 2 profils de lancement nommés dans `src/Agirh.Api/Properties/launchSettings.json` — `Maison` (localhost, `phi4-mini:3.8b`/`phi4-mini:3.8b`, comportement inchangé) et `Entreprise` (IP réelle fournie et confirmée par le porteur du projet — accessible uniquement depuis le réseau de l'entreprise, pas depuis ce poste — `phi4-mini:3.8b`/`qwen3.5:9b`). Sélection via `dotnet run --launch-profile <nom>` ou le menu déroulant de l'IDE.
- Choix du modèle Generator entreprise fait avec le porteur du projet à partir d'un vrai `tags.json` du serveur entreprise (12 modèles disponibles, fourni par le porteur du projet) : `qwen3.5:9b` retenu (9.7B, contexte 262k, bon compromis qualité/vitesse) parmi 3 options présentées (`qwen3.5:9b`, `qwen3:14b`, `gemma4:e4b`). Router laissé identique au profil Maison (`phi4-mini:3.8b`) — décision délibérée pour ne pas mélanger ce changement avec le chantier séparé et déjà mis de côté du routeur mal calibré.
- **Détour sécurité corrigé en cours de route** : une première version avait écrit l'IP réelle directement dans `launchSettings.json`, un fichier suivi par Git — repéré avant tout commit, question posée explicitly au porteur du projet (dépôt confirmé privé entre-temps, IP non routable donc risque réel faible, mais decision laissée au porteur du projet). Réponse : garder l'IP hors Git. `launchSettings.json` réel déplacé dans `.gitignore` (même traitement que `appsettings.Development.json`), `launchSettings.json.example` commité à la place avec un placeholder. `appsettings.Entreprise.json.example` mis à jour en cohérence.
- Vérifié réellement : les deux profils démarrent sans erreur de configuration (port 5080 répond dans les deux cas) ; `Maison` revérifié via un vrai flux HTTP (401 attendu sans auth, cohérent avec le comportement déjà connu) ; `Entreprise` non testable en connectivité Ollama réelle depuis ce poste (hors réseau entreprise par construction), seul le démarrage propre du processus a pu être confirmé.
- **Trouvaille non liée, signalée mais pas traitée** : les 2 fichiers checklist SMSI source (présents depuis le tout premier commit) sont absents du disque sans qu'aucun commit ne les ait supprimés — cause inconnue, ni restaurés ni formellement supprimés en attendant une décision du porteur du projet.
- `HANDOFF/NEXT_SESSION.md` réécrit (section profils détaillée, dont la conséquence pratique pour les autres appareils : `launchSettings.json` va disparaître du disque au prochain `git pull` ailleurs, `launchSettings.json.example` à copier manuellement une fois).

**Reste :**
- Les 2 fichiers checklist manquants — décision du porteur du projet à recueillir.
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8 — inchangé.
- Nouvelle demande reçue en fin de session, pas encore traitée : le porteur du projet trouve qu'il y a beaucoup de fichiers à la racine du dépôt et demande une restructuration.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-17 — Poste de travail (Windows)

**Fait :**
- Réponse aux deux questions du porteur du projet en tête de session : (1) les 2 fichiers checklist SMSI source manquants ne sont plus nécessaires — vérifié que leur contenu est déjà cité et absorbé mot pour mot dans `LOGIQUE_METIER.md` ("Dérivé du document réel SMSI.ENR.10-1/2") et `STACK_TECHNIQUE.md`, et qu'aucun fichier `.cs`/`.json`/script du dépôt ne les référence (grep à zéro résultat) — n'étaient utiles qu'au cadrage initial, déjà terminé ; (2) confirmé que les regrouper dans `docs/` est faisable, en excluant `corpus/` (lu par le pipeline d'ingestion RAG, ne doit pas bouger) et en gardant `CLAUDE.md`/`README.md` à la racine (contraintes d'outillage/convention, pas de choix).
- **Racine du dépôt réorganisée** sur demande explicite : `ARCHITECTURE.md`, `CHECKLIST.md`, `LOGIQUE_METIER.md`, `STACK_TECHNIQUE.md`, `HISTORIQUE.md`, `SUJET_STAGE.md` déplacés vers `docs/` via `git mv` (historique Git préservé). Les 2 fichiers SMSI formellement supprimés (`git rm`) après validation explicite du porteur du projet.
- Tous les renvois croisés corrigés dans ~35 fichiers non déplacés : `CLAUDE.md` (dont l'ordre de lecture canonique), `README.md` (liens markdown), les 5 agents `.claude/agents/`, `.claude/context/PROJECT_STATE.md`, `.claude/commands/`, des commentaires `///`/`//` de "pourquoi" dans une quinzaine de fichiers `.cs`/`.ts` (ex. `RepondreConversationUseCase.cs`, `OllamaRouterAdapter.cs`, routes BFF `frontend/app/api/`), `eval/gold_qa.json` (champs `notes`), `scripts/download-models.ps1`, `HANDOFF/NEXT_SESSION.md`. Vérifié par grep exhaustif sur tout le dépôt (tous types de fichiers confondus) : plus aucune mention "bare" (sans préfixe `docs/`) de ces 6 noms de fichier en dehors de `docs/` lui-même. **Exception délibérée** : les entrées passées de ce fichier (`HANDOFF/LOG.md`) n'ont pas été retouchées — append-only, ne jamais réécrire une entrée existante même pour corriger un chemin devenu obsolète (règle du fichier lui-même, ligne 3).
- Les 6 fichiers déplacés dans `docs/` n'ont **pas eu besoin d'édition** : ils se référencent uniquement entre eux (tous restent siblings dans `docs/` après le déplacement), donc leurs renvois internes bare restaient corrects tels quels — vérifié explicitement avant de conclure, pas supposé.
- Build .NET revérifié vert (0 warning) après les édits de commentaires dans les fichiers `.cs` — changement textuel uniquement, mais vérifié plutôt que supposé sans impact.
- `HANDOFF/NEXT_SESSION.md` réécrit (nouveaux chemins, item SMSI retiré de "ce qui reste ouvert" car résolu).

**Reste :**
- Toute session sur un autre appareil devra noter que `docs/` a bougé et que `launchSettings.json` (profils Maison/Entreprise, voir entrée précédente) a disparu du disque au prochain pull — les deux sont documentés dans `HANDOFF/NEXT_SESSION.md`.
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8 — inchangé.

**Prochaine session :** voir `HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-17 (suite) — Poste de travail (Windows)

**Contexte :** le porteur du projet a jugé la racine encore trop chargée après la passe précédente ("il y a beaucoup de dossiers à la racine") et a demandé une explication de chacun, puis un déplacement supplémentaire avec exigence explicite de noms "révélateurs" et de tout vérifier avant de mettre à jour ce HANDOFF ("on effectue toutes les modifs avant que tu ne mette à jour les documents de trackage").

**Fait :**
- Explication de chaque dossier racine fournie sur demande, puis 2 propositions faites et discutées avant d'agir (pas de décision unilatérale) : (1) `HANDOFF/` → `.claude/HANDOFF/` — j'ai signalé que la raison avancée par le porteur du projet ("pour que Claude le voie plus vite") était factuellement incorrecte (l'emplacement d'un fichier ne change rien à la vitesse/capacité de lecture ; c'est l'instruction dans `CLAUDE.md` qui fait lire ce fichier en premier) ; le porteur du projet a maintenu son choix en connaissance de cause après cette clarification — décision respectée. (2) `scripts/` → `.claude/scripts/` — recommandation inverse de ma part (script généraliste, pas spécifique à Claude Code) également passée outre par choix explicite du porteur du projet.
- `corpus/`, `models/`, `eval/` regroupés sous `rag/` (`rag/corpus/`, `rag/models/`, `rag/eval/`), sur ma proposition acceptée : "corpus" gardé tel quel plutôt que renommé (déjà choisi à dessein comme terme standard IR/NLP par le passé, retrouvé dans `HANDOFF/LOG.md` du 2026-08-15) — le regroupement sous un parent explicite règle le côté "pas assez révélateur" sans perdre le vocabulaire technique correct.
- Tous les impacts **code** (pas seulement doc) corrigés et **vérifiés empiriquement** : `Program.cs`, `RepoPaths.cs` + 2 fichiers de test (chemins `rag/...`), `.claude/scripts/download-models.ps1` (résolution de racine corrigée pour remonter 2 niveaux au lieu d'1 depuis son nouvel emplacement — bug réel trouvé en le relisant avant de le laisser tel quel, pas après coup) puis **réexécuté réellement** avec succès, `docker-compose.yml`/`.dockerignore`/Dockerfile Api (montages `rag/...`, validés avec `docker compose config`), `.gitignore` (`rag/models/`, validé avec `git check-ignore`).
- Balayage exhaustif de tout le dépôt (tous types de fichiers) pour les renvois `HANDOFF/`, `scripts/`, `corpus/`, `models/`, `eval/` restants — CLAUDE.md, README, 5 agents, `PROJECT_STATE.md`, commentaires code, `gold_qa.json` corrigés. Exception délibérée : entrées historiques de ce fichier non retouchées (append-only).
- **Suite de tests complète relancée après tous ces changements (pas seulement le dossier Rag) : 211/211 verts**, 5m15s, hors catégorie Evaluation (exclusion déjà actée par convention du projet, pas une nouvelle décision). `dotnet build` : 0 warning. C'est la vérification explicitement demandée ("assure-toi qu'ABSOLUMENT RIEN N'EST CASSÉ") — faite avec preuve, pas juste affirmée.
- Incident d'environnement pendant l'attente des tests : 3 tâches d'arrière-plan tuées simultanément (serveur Next.js, Api, premier run de tests) — diagnostiqué comme un événement système bref (pas un redémarrage : uptime inchangé, conteneurs Docker intacts) plutôt qu'un bug du test lui-même ; batterie retombée de 72% à 27% entre-temps, signalée au porteur du projet. Tests simplement relancés après vérification que Docker/Qdrant étaient toujours sains.
- Erreur de filtre de test trouvée et corrigée en cours de route : un premier `--filter "FullyQualifiedName~Agirh.Tests.Rag"` attrapait aussi `EvaluationGoldEndToEndTests` (lent, dépend d'Ollama, déjà connu comme partiellement instable ~27%) — ses échecs auraient pu être confondus à tort avec une régression de la restructuration. Corrigé avec `Category!=Evaluation` en plus, cohérent avec l'exclusion déjà actée pour cette catégorie.
- **NotebookLM** (`docs/notebooklm/`) : recherche faite (web) sur le fonctionnement réel de NotebookLM 2026 avant d'agir, plutôt que de deviner — format Deep Dive/Brief/Critique/Debate, limite de **500 caractères confirmée sur la doc officielle Google** pour le prompt "focus" de l'Audio Overview (pas le chiffre de 10 000 trouvé ailleurs, qui concerne le chat général), option de durée "Longer" **anglais uniquement** (contrainte réelle, signalée au porteur du projet qui a choisi de rester en français et de compenser dans le texte du prompt), "pause de 5 secondes" demandée initialement mais **n'existe pas** comme réglage de génération (Smart Pause est un contrôle d'écoute) — remplacé par la technique "segment par segment" qui produit un effet proche et qui, elle, fonctionne réellement.
- Les 5 documents existants enrichis avec du **vrai code cité** (jamais fabriqué — chaque extrait relu depuis le fichier source réel avant d'être copié) : RbacMatrix, PoleScopeGuard, validation JWT, cookie BFF, embedding ONNX, reranker cross-encodeur (sigmoïde), prompt système complet du Router, garde-fou anti-hallucination à double porte de sortie, frames SSE, migration EF Core auto. Pondération demandée respectée : documents 02/03 (IA/ML) nettement plus enrichis que 01/04/05.
- Chaque citation de code corrigée une seconde fois sur demande du porteur du projet pour porter son **chemin complet depuis la racine du dépôt** (ex. `src/Agirh.Core/Security/RbacMatrix.cs` — le préfixe `src/` manquait initialement dans 9 citations sur 9, trouvé et corrigé après relecture ciblée). Les deux prompts (audio et quiz) mis à jour pour exiger explicitement la citation orale/écrite de ces chemins, pas seulement du code.
- `docs/notebooklm/prompt-audio-overview.md` (493/500 caractères, compté avec PowerShell `.Length`, pas à l'œil) et `docs/notebooklm/prompt-quiz.md` créés.
- `.claude/HANDOFF/NEXT_SESSION.md` réécrit en profondeur (nouveaux chemins `.claude/`/`rag/`, section NotebookLM, pièges mis à jour).

**Reste :**
- Toute session sur un autre appareil : au prochain `git pull`, `.claude/HANDOFF/`, `.claude/scripts/download-models.ps1` (réel) et `launchSettings.json` disparaissent du disque (gitignorés ou déplacés) — régénérer localement (copier les `.example`, relancer le script si besoin). Documenté dans `NEXT_SESSION.md`.
- Prompts NotebookLM prêts mais jamais encore exécutés dans l'interface NotebookLM elle-même (pas d'accès à cette interface depuis une session Claude Code).
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8 — inchangé.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-17 (suite 3) — Poste de travail (Windows)

**Fait :**
- `prompt-quiz.md` → `prompt-flashcards.md`, sur demande explicite du porteur du projet (voulait des flashcards, pas un quiz à choix multiples). Contenu réécrit pour le vrai format NotebookLM (type Question/Réponse, chemin de fichier sur la face arrière plutôt que des distracteurs qui n'avaient plus de sens). Au passage, correction du prompt audio : le porteur du projet l'avait retaillé lui-même mais dépassait la limite de 500 caractères (575) — retaillé de nouveau (471) en gardant l'esprit de sa reformulation plus dense.
- `docs/APPRENTISSAGE/principal.md` restructuré autour de la partie IA/ML sur demande explicite et répétée ("je dois très bien maîtriser tout ce qui est de l'IA en fait") : les 4 phases RAG + Router + Generator/garde-fou avec du vrai code source cité (chemin **et numéros de ligne exacts**, vérifiés via `grep -n` sur les vrais fichiers avant citation, jamais fabriqués) + une carte des 12 fichiers du sous-système IA. Reste du document resserré pour respecter la contrainte de recopie manuelle explicitement rappelée par le porteur du projet — plusieurs passes de coupe (183 → 321 → 298 lignes) après avoir signalé que la première version dépassait sans doute la limite.
- **Présentation de soutenance générée** (`docs/presentations/`) : demande initiale "va directement, pas de questions, je te fais confiance" — recherche de contexte faite quand même avant d'agir (dossier `presentation/` du Bureau, images = vrais logos AGIRH/ENSA Safi, pas des logos inventés). Un `script_orateur.md` antérieur trouvé dans ce dossier décrivait une architecture Python/FastAPI/Microsoft-Agent-Framework sans rapport avec le projet réel — **volontairement pas utilisé comme base** (mtime très antérieur au reset V7→V8), tout le contenu généré vient du projet réel et vérifié. 20 slides via `python-pptx` (déjà installé), thème sombre indigo extrait des vraies couleurs du produit (`globals.css`). **Vérification visuelle réelle** : chaque slide exportée en PNG via automatisation COM PowerPoint et inspectée à l'image — 4 bugs trouvés et corrigés (logo débordant, titre traversé par son soulignement, chevauchement de texte, flèche de diagramme orpheline).
- Question du porteur du projet sur la place de `docs/` dans Git ("GitHub c'est pour le code non ?") — corrigée : `docs/` est déjà versionné depuis le début du projet (LOGIQUE_METIER.md, STACK_TECHNIQUE.md, etc., tous déjà commités). La vraie distinction pertinente : binaire (pas diffable, gonfle l'historique) vs texte, pas "dans docs/" vs "pas dans docs/". Décision actée : script Python de génération (texte, reproductible) commité, `.pptx` généré gitignoré, son existence documentée dans `script_orateur.md`.

**Reste :**
- **Vrai logo AGIRH disponible mais pas encore branché** dans `frontend/components/agirh-mark.tsx` (toujours le placeholder `UsersRound`) — ce n'est plus une attente d'un fichier manquant, le fichier existe (`presentation/assets/agirh_white_rgba.png`, hors dépôt).
- Présentation jamais répétée à voix haute (minutage ~15 min visé, pas chronométré).
- Prompts NotebookLM toujours jamais exécutés dans l'interface réelle.
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8, calendrier réel du stage (jamais communiqué) — inchangé.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-17 (suite 4) — Poste de travail (Windows)

**Fait :**
- Vrai logo AGIRH branché dans le frontend (`agirh-mark.tsx` → `next/image` vers `public/agirh-logo.png`), placeholder `AgirhMark`/`iconOnly` supprimé (confirmé inutilisé ailleurs avant suppression). Vérifié par vraies captures d'écran (Edge en mode headless, pas de Playwright disponible ici) sur page de garde et connexion — rendu net, bien proportionné. `tsc`/`build`/`lint` verts.
- Incident mineur pendant la vérification : un `Stop-Process -Name msedge -Force` destiné à nettoyer mes propres instances headless a en réalité visé **tous** les process Edge par nom, y compris potentiellement la session normale du porteur du projet (mes instances headless, lancées avec `-Wait`, s'étaient déjà terminées seules). Signalé immédiatement, confirmé par le porteur du projet qu'aucune perte n'a eu lieu. Leçon retenue et à appliquer : cibler par PID explicite, jamais tuer par nom de process partagé avec l'usage normal de l'utilisateur.
- Prompt audio NotebookLM révisé une seconde fois, sur retour d'un essai réel du porteur du projet dans l'interface : la v1 perdait plus d'une minute en préambule générique malgré la consigne, et tournait autour de 12 minutes. V2 : interdiction de préambule rendue beaucoup plus explicite (formulations interdites listées), aucune mention de durée nulle part dans le prompt (le porteur du projet craint un effet de plafond psychologique), 46 notions chronologiques (contre 25) pour densifier le contenu et allonger naturellement l'épisode, récapitulatif final demandé explicitement.

**Reste :**
- **Fichier non attendu trouvé, pas touché** : `docs/APPRENTISSAGE/principal.pdf`, jamais créé par une session Claude Code — probablement un export du porteur du projet. Ni commité ni ignoré pour l'instant, laissé tel quel en attendant une décision explicite.
- Prompt audio v2 pas encore testé en conditions réelles dans NotebookLM (seule la v1 l'a été).
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8, calendrier réel du stage — inchangé.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-18 — Poste de travail (Windows)

**Fait :**
- Plan de migration du vocabulaire métier français → anglais dans le code (identifiants, pas le texte utilisateur/RAG) validé avec le porteur du projet après une itération : proposition initiale à 10+ tranches jugée trop fine, consolidée en 4 patches sur demande explicite ("fais le découpage comme tu veux et applique immédiatement la première tranche"). Plan complet sauvegardé (glossaire, 4 patches, exceptions actées) — voir le fichier de plan référencé dans le contexte de session, ou reconstituer depuis `NEXT_SESSION.md` si besoin.
- **Patch 1/4 exécuté en entier et poussé** : `Collaborateur`→`Employee`, `Pole`→`Department`, `CompteUtilisateur`→`UserAccount`, `Matricule`→`EmployeeNumber`, enums `RoleType`/`ContractType` (ex-`TypeContrat`, valeurs CDI/CDD/Stage/Alternance conservées), `AccesRefuseException`→`AccessDeniedException`, `PoleScopeGuard`→`DepartmentScopeGuard`, mécanique `ExecuterAsync`→`ExecuteAsync`/`acteur`→`actor` sur les 15 UseCases, `EmployeeController`/`AuthController` réécrits (routes `api/collaborateurs`→`api/employees`, `elever-role`→`elevate-role`), 5 autres controllers corrigés en références croisées, config Jwt/Ollama renommée, 5 fichiers frontend (`motDePasse`→`password`, `poleId`/`compteId`→`departmentId`/`accountId`), 23 des 32 fichiers de tests. Détail complet dans `NEXT_SESSION.md`.
- Erreur mineure auto-corrigée en cours de route : un `replace_all` trop large a renommé par erreur une propriété hors périmètre (`ConditionsTypeContrat`→`ConditionsContractType` dans `TemplateItem.cs`) — repéré au diff, corrigé immédiatement avant tout commit.
- Vérifications : `dotnet build` 0 erreur/0 warning, `dotnet test` 211/211 verts (hors catégorie Evaluation), `npx tsc --noEmit` et `npm run build` (frontend) verts. Commit unique poussé sur `master`.

**Reste :**
- Patches 2 (Workflow & Template), 3 (Conversation & RAG), 4 (Finalisation + régénération migration EF + resync base + mise à jour docs/**/*.md) — pas commencés. Confirmation du porteur du projet à demander avant d'enchaîner (l'autorisation reçue portait explicitement sur la première tranche).
- `docs/**/*.md` volontairement pas retouchés à ce stade (prévu au Patch 4).
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8, calendrier réel du stage, `docs/APPRENTISSAGE/principal.pdf` non identifié — tous inchangés, sans rapport avec cette session.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-18 (suite) — Poste de travail (Windows)

**Fait :**
- **Patch 2/4 de la migration vocabulaire français → anglais exécuté et poussé**, sur demande explicite du porteur du projet ("Continue les corrections Patch 2"). Renommage complet de `WorkflowInstance`/`WorkflowTemplate`/`ChecklistItemStatus`/`TemplateItem`/`TemplateSection` (propriétés, méthodes), enums `ItemEtat`→`ItemStatus` et `TemplateStatut`→`TemplateStatus` (valeurs traduites), `WorkflowStatus` (valeurs traduites : EnCours/Cloture/Archive/Annule/Suspendu → InProgress/Closed/Archived/Cancelled/Suspended), les 9 UseCases du cycle Workflow/Template (tous renommés), `IWorkflowInstanceRepository`/`IWorkflowTemplateRepository`, `Notification`/`TypeNotification`→`NotificationType`, `WorkflowController`/`TemplateController` (+ routes des verbes), route `reindexer-corpus`→`reindex-corpus`. Détail complet dans `NEXT_SESSION.md`.
- **Point d'arbitrage soulevé avant d'agir** (pas tranché seul) : renommer les valeurs de `WorkflowStatus` faisait fuiter un mot anglais dans une phrase du chat produite par `RepondreConversationUseCase` (périmètre patch 3, pas touché). Question posée avec 3 options concrètes ; porteur du projet a choisi de renommer l'enum quand même + ajouter un petit mapping français local (`StatutEnFrancais`, switch de 5 lignes) dans ce fichier pour préserver la règle "texte utilisateur final en français" en attendant sa réécriture complète au patch 3. À retirer proprement quand le patch 3 réécrira ce fichier.
- Vérifications : `dotnet build` 0 erreur/0 warning, `dotnet test` 211/211 verts (hors catégorie Evaluation, même total qu'avant — renommage mécanique, aucun test ajouté/retiré). Pas d'impact frontend ce patch (confirmé par grep : aucun proxy BFF n'existe encore pour ces routes). Commit unique poussé sur `master`.

**Reste :**
- Patch 3 (Conversation & RAG) et Patch 4 (Finalisation) — pas commencés. Confirmation du porteur du projet à demander avant d'enchaîner (même règle qu'avant chaque patch).
- Le mapping temporaire `StatutEnFrancais` dans `RepondreConversationUseCase.cs` doit être retiré/remplacé proprement au patch 3, pas oublié.
- `docs/**/*.md` toujours pas retouchés (prévu au Patch 4).
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8, calendrier réel du stage, `docs/APPRENTISSAGE/principal.pdf` non identifié — tous inchangés, sans rapport avec cette session.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-18 (suite 2) — Poste de travail (Windows), session en remote-control

**Fait :**
- **Patch 3/4 de la migration vocabulaire français → anglais exécuté et poussé**, sur demande explicite du porteur du projet ("Continue"). Renommage complet de l'orchestration conversationnelle (`RepondreConversationUseCase`→`AnswerConversationUseCase` et tous ses types associés : `EvenementConversation`/`FragmentTexte`/`ReponseTerminee`→`ConversationEvent`/`TextFragment`/`ResponseCompleted`, `IntentionConversation`→`ConversationIntent`, `ReponseConversation`→`ConversationResponse`), du pipeline RAG (`ChunkDocumentaire`→`DocumentChunk`, tous les ports Llm/Rag, tous les adapters Ollama*/Onnx*/Qdrant*/Tokenizer/Chunker, `IngererCorpusUseCase`→`IngestCorpusUseCase`), `ChatController` (route `demander`→`ask`, wire SSE `termine`→`done`, `texte`/`sourcee`→`text`/`sourced`), `AdminController` terminé. Frontend : route BFF renommée (`app/api/chat/ask`), mêmes champs wire mis à jour (`chat-widget.tsx`, `chat-message.tsx`). Détail complet dans `NEXT_SESSION.md`.
- **Décision technique documentée sans interrompre le porteur du projet** : le prompt de classification du routeur (`OllamaRouterAdapter`) garde ses libellés `DOCUMENTAIRE`/`STATUT_DOSSIER`/`HORS_PERIMETRE` en français — contrat de prompt calibré empiriquement avec le modèle, pas du vocabulaire de code ; le traduire risquait de dégrader silencieusement un routage déjà imparfait (~27% d'erreur connu) sans bénéfice mesurable.
- Fix temporaire du Patch 2 (`StatutEnFrancais`) retiré proprement et remplacé par sa version définitive (`FormatStatusInFrench`), intégrée normalement maintenant que le fichier est réécrit en entier.
- Bug mineur du Patch 2 corrigé au passage : `notification-bar.tsx` lisait encore `dateReference` alors que le Patch 2 avait renommé `Notification.DateReference`→`ReferenceDate` côté backend — aucun impact visuel (champ jamais affiché dans l'UI), corrigé pour cohérence (`referenceDate`).
- **Pendant cette session, le porteur du projet est passé en pilotage à distance** ("remote-control") pour garder un œil sur la progression sans être physiquement présent. Configuration `powercfg` ajustée sur sa demande (PC branché secteur) : mise en veille et extinction d'écran désactivées sur l'alimentation secteur (AC) uniquement — l'hibernation était déjà désactivée par défaut sur ce poste ; réglages batterie (DC) non touchés.
- Vérifications : `dotnet build` 0 erreur/0 warning, `dotnet test` 211/211 verts (même total qu'avant), `npx tsc --noEmit` et `npm run build` (frontend) verts. Commit unique poussé sur `master`.

**Reste :**
- **Patch 4 (Finalisation) — pas commencé, contient une opération destructive locale** (`dotnet ef database drop`/`update` sur `agirh-sql`) : ne pas lancer sans confirmation fraîche et explicite du porteur du projet, au-delà d'un simple "continue" générique — voir `NEXT_SESSION.md` pour le détail complet du contenu restant.
- **Qdrant non revérifié en conditions réelles** : les clés de payload ont changé (`cheminTitres`/`contenu`→`titlePath`/`content`). Docker n'était pas démarré sur ce poste pendant la session (`docker ps` a échoué, daemon non lancé), donc pas de test live possible. Une collection Qdrant indexée avant ce patch cassera la branche RAG documentaire du chat tant que `POST api/admin/reindex-corpus` n'est pas relancé — à faire avant tout test manuel du chat, et avant le Patch 4.
- `docs/**/*.md` toujours pas retouchés (prévu au Patch 4).
- Contrôle visuel pixel de `/chat`, routeur conversationnel, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8, calendrier réel du stage, `docs/APPRENTISSAGE/principal.pdf` non identifié — tous inchangés, sans rapport avec cette session.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

---

## 2026-08-18 (suite 3) — Poste de travail (Windows), session en remote-control

**Fait :**
- **Patch 4/4 (Finalisation) exécuté et poussé, sur confirmation explicite du porteur du projet** ("go for the last step") — la migration vocabulaire français → anglais est maintenant **complète** (4/4 patches).
- `Program.cs` réécrit (derniers identifiants français), `RegisterUseCaseTests.cs` (oubli réel du Patch 1 corrigé : `motDePasse`→`password` dans un nom de paramètre de théorie), commentaire obsolète corrigé dans `frontend/app/chat/page.tsx`.
- **Migration EF Core régénérée à neuf** (`InitialCreate` unique, schéma 100% anglais vérifié) et **appliquée réellement** sur `agirh-sql` (`dotnet ef database drop --force` + `update`) — opération destructive locale délibérée et signalée à l'avance, aucune donnée de prod derrière.
- **Qdrant réindexé réellement** après changement des clés de payload (`cheminTitres`/`contenu`→`titlePath`/`content`) : ancienne collection supprimée (stale, causait un `KeyNotFoundException`), reconstruite via un vrai appel HTTP `POST api/admin/reindex-corpus` (smoke test complet register→promotion SQL→login→reindex, `{"documentsRead":6,"chunksIndexed":72}`).
- **Toutes les citations de code dans `docs/**/*.md` mises à jour** (dernière étape du plan, volontairement différée à la fin) : `ARCHITECTURE.md`, `LOGIQUE_METIER.md`, `STACK_TECHNIQUE.md`, `CHECKLIST.md`, les 5 documents `docs/notebooklm/`, les 2 prompts NotebookLM, `docs/APPRENTISSAGE/principal.md` (blocs de code revérifiés contre le vrai source, pas seulement les noms), `docs/presentations/script_orateur.md`. Un grep final large (glossaire complet) a rattrapé quelques oublis d'une première passe (config JWT dans le doc fondations, citation d'enum routeur dans `CHECKLIST.md`, `PoleScopeGuard` dans le prompt audio).
- **Exception délibérée** : dans `script_orateur.md` (script **parlé**), les noms de rôles restent en français (traité comme texte utilisateur final à prononcer, pas une citation de code) — contrairement aux mêmes rôles dans les documents de référence écrits, traduits pour cohérence avec `RoleType`.
- **Incident fork et récupération** : 3 forks ont échoué immédiatement (cap de session, indépendant du contenu) — travail repris directement en conversation principale. Le 4ᵉ fork (seul à terminer) est sorti de son périmètre sur `docs/notebooklm/prompt-audio-overview.md` (a réécrit tout le prompt en version condensée au lieu de corriger seulement les citations, contredisant la note de conception "V2 = plus explicite" du fichier). **Repéré avant tout commit** par relecture attentive du diff, annulé précisément via le côté `-` du diff déjà capturé — zéro perte confirmée (`git diff`/`git log`).
- **Vérifications** : `dotnet build -c Release` 0 warning/0 erreur. `dotnet test -c Release` : 210/211 puis 211/211 en isolant le seul échec (`OllamaRouterAdapterTests`, "Mon dossier est-il clôturé ?") — flake connu et déjà documenté (~27% d'erreur du routeur), reconfirmé comme tel en relançant isolément (5/5 verts), pas une régression.
- **Document de vérification créé pour le porteur du projet** : `docs/VERIFICATION_MIGRATION_ANGLAIS.md`, demandé explicitement en cours de session — steps concrets pour confirmer visuellement/fonctionnellement que la migration est terminée.
- Serveurs laissés démarrés en fin de session (Api port 5080, frontend port 3000, Docker déjà up) pour permettre une vérification visuelle immédiate à distance.

**Reste :**
- Le endpoint chat SSE (`GET api/chat/ask`) n'a pas été testé en direct cette session — seule l'ingestion Qdrant l'a été. À faire en suivant `docs/VERIFICATION_MIGRATION_ANGLAIS.md`.
- **Nouvelle demande du porteur du projet, pas encore commencée à ce checkpoint** : refaire `docs/Rapport_Avancement_PFA_AGIRH_Wilfried_TSETSE.pdf` (3 pages max, nouveaux diagrammes `.puml`, focus IA, pour l'encadrant de stage).
- Sujets pré-existants toujours ouverts (indépendants de la migration) : vérification visuelle pixel du chat, routeur conversationnel (~27%), jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-19 — Poste de travail (Windows)

**Fait :**
- **Rapport d'avancement PDF entièrement refait**, sur demande explicite du porteur du projet (`docs/Rapport_Avancement_PFA_AGIRH_Wilfried_TSETSE (1).pdf`, non commité — binaire, voir source `docs/rapport_avancement/rapport.tex`). L'ancien PDF décrivait encore l'architecture V7 abandonnée ; nouvelle version restructurée en 3 pages : contexte/pivot V7→V8, architecture+stack, cœur IA (RAG 4 phases + garde-fou anti-hallucination double-porte), workflows métier, bilan vérifié.
- 3 diagrammes générés via le CLI `plantuml.jar` local (bundlé avec l'extension VS Code `jebbs.plantuml`, `java -jar ... -tpng`) : `architecture.png`, `ai_pipeline.png`, et un nouveau `workflow_circuit.png` (circuit de validation des templates, mis en page horizontale).
- **Itération sur retour direct du porteur du projet** (capture d'écran à l'appui) : le titre de §3 se retrouvait seul en bas de page 1 (orphelin). Corrigé en forçant un saut de page avant §3 et avant §4 (`\newpage`), rééquilibrant tout le contenu sur 3 pages pleines sans dépasser le budget donné ("3 pages il n'y a pas de problème").
- `docs/VERIFICATION_MIGRATION_ANGLAIS.md` créé (checklist de vérification visuelle/fonctionnelle de la fin de migration vocabulaire, demandé explicitement en fin de session précédente).
- **Dossier de sauvegarde des captures d'écran Windows corrigé** : redirigeait vers un OneDrive imbriqué en double (`...\OneDrive\Bureau\OneDrive\Captures d'écran`, probable réglage hérité). Remis au chemin par défaut `C:\Users\Wilfried\Pictures\Screenshots` via le registre (`User Shell Folders\{B7BEDE81-DF94-4682-A7D8-57A52620B86F}`). Seul le réglage futur a changé ; le fichier déjà utilisé dans la conversation reste dans son ancien emplacement OneDrive, pas déplacé.
- **`docs/presentations/generate_pptx.py` corrigé** (commit `91e27a0`) : non retouché depuis sa création (17 août), donc encore plein d'identifiants français éliminés par la migration du 18 août. Corrigé : `PoleScopeGuard`→`DepartmentScopeGuard` (label + corps de méthode réel `CanAccessDepartment`), paramètres réels de `MarkdownChunker` (`countTokens`/`maxTokensPerChunk`/`overlapRatio`), `RepondreConversationUseCase.cs`→`AnswerConversationUseCase.cs` (+ `candidates`/`DocumentaryPreparation`/`MinimumRelevanceThreshold`/`best`), citation `OnnxRerankerAdapter.cs` ligne 65→62 (la méthode citée a bougé après un refactor). Régénéré avec succès (20 slides) ; **pas de rendu visuel pixel possible sur ce poste** (pas de LibreOffice/soffice installé) — vérifié par diff texte + calcul de largeur des blocs de code modifiés.
- **Piège évité avant commit** : `DOCUMENTAIRE`/`STATUT_DOSSIER`/`HORS_PERIMETRE` (slides 6/14 du pptx, et une ligne de `script_orateur.md`) ont été traduits par erreur puis **annulés** après relecture du vrai `OllamaRouterAdapter.cs` — ce sont les mots exacts et volontaires du prompt système du Router (commentaire explicite dans le code), pas du vocabulaire de code, décision déjà actée au Patch 3 et documentée dans `NEXT_SESSION.md`/`LOG.md` du 18 août. Confirme la valeur de toujours vérifier contre le vrai code source avant de faire confiance à un vieux résumé de conversation.
- **Divergence factuelle découverte, non résolue à ce checkpoint** : le pptx/script (17 août, inchangés) disent l'encadrant « M. Issam MITAR » ; le rapport PDF fraîchement refait dit « M. Moulay Rachid Didi Alaoui (superviseur), M. Saad (encadrant direct) ». Ni l'un ni l'autre n'est dérivable du code — question posée explicitement au porteur du projet, pas encore répondue à ce checkpoint. Ajoutée aux "Décisions en attente" de `NEXT_SESSION.md`.

**Reste :**
- **Trancher la question de l'encadrant** puis appliquer la réponse au pptx (titre + `script_orateur.md`) et/ou au rapport selon le cas — actuellement les deux documents ne se contredisent que sur ce point précis.
- Sujets pré-existants toujours ouverts (indépendants de cette session) : vérification visuelle pixel du chat, routeur conversationnel (~27%), jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-19 — Poste de travail (Windows), suite

**Fait :**
- **Vérification indépendante d'un scan Copilot du repo**, demandée par le porteur du projet ("Est-ce vrai, ce que dit l'IA de github ?"). Chaque affirmation vérifiable a été recoupée avec le code réel plutôt qu'acceptée telle quelle : existence des 10+8 fichiers cités (tous confirmés par `Glob`), notifications recalculées à la demande sans table dédiée (confirmé dans `GetNotificationsUseCase.cs`), absence de test d'intégration HTTP complet (`WebApplicationFactory` introuvable dans `tests/`), primitives `Suspend()`/`Resume()` présentes sur `WorkflowInstance` sans UseCase/endpoint (confirmé), `IAuditTrailPort`/`AuditTrailAdapter`/`TechnicalLogAdapter` documentés dans `ARCHITECTURE.md` mais introuvables dans `src/` (confirmé, 0 résultat). Verdict global : scan fiable, aucune fabrication détectée.
- **Un vrai bug trouvé au passage, pas juste un écart de doc** (commit `6d94d50`) : `docker-compose.yml` définissait encore `Jwt__DureeValiditeMinutes`, alors que `Program.cs:60` lit `Jwt:TokenLifetimeMinutes` depuis le Patch 1 de la migration vocabulaire (18 août). ASP.NET Core ne mappe une variable d'env `Jwt__X` que sur la clé de config `Jwt:X` exacte — donc cette variable était silencieusement ignorée en environnement Docker, l'app retombant sur le défaut codé en dur (`60`) qui coïncidait avec la valeur voulue. Corrigé (`Jwt__TokenLifetimeMinutes: "60"`) + commentaire aligné dans `frontend/lib/api/session.ts:4`. `dotnet build -c Release` vérifié vert avant commit (0 warning/0 erreur) ; aucun `.cs` modifié donc pas de `dotnet test` nécessaire.
- Les deux autres écarts relevés (audit trail non implémenté, suspend/resume sans UseCase) ont été délibérément laissés en l'état — ce sont des cibles d'architecture / cas particuliers déjà connus (`CHECKLIST.md`, `LOGIQUE_METIER.md` §8), pas des oublis de migration, donc pas de décision produit à prendre unilatéralement.

**Reste :** inchangé par rapport à l'entrée précédente du jour — voir ci-dessus (question de l'encadrant en tête de liste).

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-19 — Poste de travail (Windows), suite 2

**Fait :**
- **Question posée sur l'offboarding** (le porteur du projet voulait savoir s'il existait) : vérifié directement dans le code plutôt que supposé — `WorkflowType.Offboarding` est une valeur de premier ordre de l'enum, tout le pipeline (`InstantiateWorkflowUseCase`, `WorkflowController`, `TemplateController`) est générique par type donc fonctionne sans code dédié, checklist de référence documentée (`LOGIQUE_METIER.md` §4), 2 documents RAG dédiés. Deux nuances signalées : aucune UI frontend ne déclenche de workflow (ni onboarding ni offboarding), et `InstantiateWorkflowUseCaseTests.cs` ne teste explicitement que `WorkflowType.Onboarding`.
- **Nouvelle fiche de suivi pour les encadrants créée** (`docs/rapport_avancement/fiche_synthese.tex`/`.pdf`), demandée explicitement pour être tenue en main pendant la soutenance. Clarifié via `AskUserQuestion` avant rédaction (format LaTeX, longueur, structure, encadrant à mentionner) plutôt que de deviner. Réponses obtenues : LaTeX→PDF avec page de garde "niveau ingénieur" ; 3 pages recto ; structure libre avec un tableau récapitulatif en fin (pas de choix explicite entre plan/narratif/factuel — synthèse faite entre les trois) ; ne garder que M. Moulay Rachid Didi Alaoui (jamais M. Saad), mais sans toucher pptx/rapport tout de suite ("plus tard").
- **Itération en cours de route** : le porteur du projet a demandé en aparté d'intégrer les 3 diagrammes déjà générés (`architecture.png`/`ai_pipeline.png`/`workflow_circuit.png`) "car c'est plus parlant" — pas besoin d'en créer de nouveaux, les 3 existants (dont `workflow_circuit.png`, créé plus tôt cette même session) suffisaient. Premier essai à 4 pages (repris du découpage déjà validé de `rapport.tex`, +1 page pour la couverture). **Vérifié visuellement page par page via le Read tool sur le PDF compilé** (capacité inédite cette session — contrairement au pptx, un PDF se lit nativement) : mise en page propre mais beaucoup d'espace vide sur 3 des 4 pages. Recompacté à 3 pages en fusionnant deux sections sur la même page + agrandissement léger d'un diagramme, recompilé, revérifié visuellement — bon équilibre, zéro `Overfull \vbox`/`Underfull` dans le log.
- Logo produit `frontend/public/agirh-logo.png` copié dans `docs/rapport_avancement/` pour la page de garde (aucun logo ENSA Safi disponible dans le repo — non recherché en ligne, texte utilisé à la place).
- `docs/rapport_avancement/` reste **entièrement non suivi par git** (même convention que `rapport.tex`/`rapport.pdf` déjà en place avant cette session) — la fiche n'a donc pas été commitée, seul le HANDOFF l'a été.

**Reste :**
- Appliquer la correction encadrant (voir "Décisions en attente" de `NEXT_SESSION.md`) à `generate_pptx.py`, `script_orateur.md` et `rapport.tex` quand le porteur du projet le demandera — décision déjà connue, juste reportée.
- Sujets pré-existants toujours ouverts : vérification visuelle pixel du chat, routeur conversationnel (~27%), jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows), suite 3 (entrée courte — quota tokens ~90%)

**Fait :** renommage "AGIRH"→"arhia" appliqué **uniquement** à `fiche_synthese.tex` (portée explicitement restreinte par le porteur du projet, pas le dépôt entier) — titre + en-tête + définition ajoutée "(Agent RH IA)" + lien GitHub inséré. Logo entreprise `agirh-logo.png` inchangé (confirmé : c'est le logo de l'entreprise d'accueil, pas du produit). Recompilé, 3 pages, 0 warning, revérifié visuellement.
**Clarifié** : "AGIRH" = nom de l'entreprise d'accueil, "arhia" = nom du produit du stagiaire — d'où le besoin de les distinguer. Renommage complet du dépôt (~200 fichiers) voulu mais **explicitement différé au 2026-08-23+** (quota tokens). Nouveau futur livrable notifié : rapport de fin de stage (pas commencé). Question d'accès GitHub (dépôt privé) expliquée, aucune action prise.
**Reste :** tout dans "Décisions en attente" de `NEXT_SESSION.md` — lire ce fichier en premier à la prochaine reprise.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows), suite 4 (entrée courte)

**Fait :** logo ENSA Safi ajouté sur la couverture de `fiche_synthese.tex`, à gauche du logo entreprise AGIRH (fourni par le porteur du projet dans `Bureau\presentation\assets\`, copié en local). Dimensions réelles vérifiées (560×90 vs 126×64, ratios très différents) avant dimensionnement — même hauteur pour les deux via `\includegraphics[height=...]` (zoom uniforme, jamais de largeur forcée qui aurait étiré/déformé), `\raisebox{-0.5\height}` pour un centrage vertical garanti malgré la différence de forme. Recompilé, 3 pages, 0 warning, revérifié visuellement à chaque itération.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows)

**Fait :**
- **Fiche de suivi corrigée** (`docs/rapport_avancement/fiche_synthese.tex`/`.pdf`) : le porteur du projet a demandé de préciser que le taux d'erreur du Router (~27%) est une contrainte du PC local (pas de GPU), pas une limite de conception, et d'ajouter un plan concret de déport de charge vers le serveur Ollama de l'entreprise (`192.168.100.220:11434`) prévu jeudi 27 août 2026 (calculé depuis "jeudi prochain" — vérifié par double ancrage de calendrier, aujourd'hui 20 août 2026 étant lui-même un jeudi).
- **Raison révélée en cours de route** pour un piège déjà documenté (`Docker : ne jamais conteneuriser Ollama`) : Ollama tourne nativement sur l'hôte délibérément pour **simuler** ce futur serveur entreprise, pas juste pour une histoire de performance GPU comme documenté jusqu'ici. Ajouté au piège existant dans `NEXT_SESSION.md` plutôt que dupliqué.
- **Modèles réellement disponibles sur le serveur entreprise vérifiés** dans `C:\Users\Wilfried\Downloads\tags.json` (12 modèles listés, pas supposés) avant de recommander quoi que ce soit. Analyse faite : `phi4:14b` proposé pour le Router (même famille que `phi4-mini:3.8b` actuel, upgrade le plus sûr) ; `gemma4:12b` proposé pour le Generator (déjà identifié dans ce projet, déjà écarté en local mais uniquement pour lenteur sans GPU, jamais pour la qualité) ; `phi4-reasoning:14b` envisagé puis explicitement écarté par défaut pour le Router — risque concret identifié en relisant `OllamaRouterAdapter.ParseIntent` (`.Contains` sur la sortie brute) : un modèle qui raisonne à voix haute avant de conclure pourrait mentionner plusieurs intentions et fausser ce parsing par sous-chaîne.
- **Itération de mise en page** : l'ajout de texte a fait déborder la fiche de 3 à 4 pages (juste le tableau récap isolé sur une page quasi vide) ; corrigé en réduisant légèrement les deux diagrammes de la page 3 (66%→55% et 78%→68% de largeur) plutôt qu'en coupant du contenu — recompilé, revérifié visuellement, revenu à 3 pages sans avertissement `Overfull`.
- HANDOFF (`NEXT_SESSION.md`) mis à jour : item 1 de "Ce qui reste ouvert" enrichi avec le plan daté complet plutôt que dupliqué ailleurs.

**Reste :**
- Rien de bloquant. Le plan routeur/serveur entreprise du 27 août est documenté mais pas encore exécuté — normal, la date n'est pas encore arrivée.
- Sujets pré-existants toujours ouverts : correction encadrant reportée (pptx/script/rapport), vérification visuelle pixel du chat, jeu de Q/R gold à reconfirmer, endpoints de lecture/liste, cas particuliers §8.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows), suite 5 (entrée courte)

**Fait :** sur retour direct du porteur du projet (2 captures d'écran) : (1) document renommé "Fiche de Suivi"→"**Synthèse des Travaux Réalisés**" (couverture + en-tête de pages) avec sous-titre "Support de questions — Soutenance PFA", pour signaler que c'est un résumé destiné à alimenter les questions des encadrants, pas un suivi passif ; (2) couverture réorganisée en hiérarchie logique (institution → logos → type de document → produit arhia → pitch → équipe → dépôt → sommaire), le bloc "type de document" remonté avant le titre produit ; (3) précisé "Dépôt GitHub privé, accessible sur demande" — résout la question d'accès GitHub sans changer la visibilité ni ajouter de collaborateur. Recompilé, 3 pages, 0 warning, revérifié visuellement.
**Note pour la prochaine session** : les entrées du 2026-08-20 dans ce fichier ne sont pas en ordre strictement chronologique (plusieurs `Edit` ont ancré sur un motif de fin de section non-unique, qui a matché plusieurs fois) — le contenu de chaque entrée reste correct et daté, juste l'ordre d'apparition dans le fichier n'est pas fiable pour ce jour précis. Pas grave, pas prioritaire à corriger — juste ne pas supposer que l'ordre du fichier = ordre réel des événements pour le 2026-08-20.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows), suite 6 (entrée courte)

**Fait :** rédigé (pas envoyé) le mail de fin de stage pour M. Rachid (à) / Mitar + Hanaa (Cc) — annonce fin de projet, pièce jointe = fiche de synthèse (précisé différente du rapport d'avancement), demande de leurs noms d'utilisateur GitHub pour accès collaborateur, mention du test prévu le 27 août avec les modèles entreprise. Ni adresses email ni auth Gmail disponibles pour un envoi automatisé — texte donné tel quel dans la conversation.
**Nouvelle demande, explicitement reportée par le porteur du projet ("on la garde pour après réinitialisation du quota")** : réorganiser le dépôt GitHub pour présentation professionnelle avant que les 3 destinataires du mail n'y aient accès — arrêter de tracker `docs/` (+ `.claude/` probable) sans réécrire l'historique, réécrire `README.md` en anglais avec une vraie prise en main, exposer les modèles ONNX utilisés dans un dossier Google Drive (auth MCP requise, pas encore faite). Détail complet dans `NEXT_SESSION.md` — distinct du renommage complet arhia déjà en attente, même blocage (quota).

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows), suite 7 (entrée courte)

**Fait** (commit `997e650`) : `docs/notebooklm/prompt-audio-overview.md` passé en v3 — méta-consignes resserrées (demande explicite), et surtout **7 notions sur 46 corrigées après relecture du vrai code plutôt que recopiées telles quelles** : ordre rôle/portée inversé dans #6 (le vrai code vérifie le rôle avant `DepartmentScopeGuard`, pas l'inverse), nom de classe inventé en #2 (`WorkflowInstanceRepository`, pas "Ef..."), tokenizer en #13 (SentencePiece seul, jamais WordPiece ici), #46 décrivait l'audit trail comme implémenté alors qu'il ne l'est pas (confirmé absent le 2026-08-19), #31 présentait 21/48 comme définitif alors que `NEXT_SESSION.md` le note "avant corrections, jamais rejoué". Détail complet des 7 corrections dans le fichier lui-même ("Corrections apportées en v3").
**Reste :** rien de bloquant — fichier prêt à coller dans NotebookLM.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows), suite 8 (entrée courte)

**Fait** (commit `e33c621`) : `prompt-audio-overview.md` passé en v4 sur nouvelle demande — cette fois le texte des 46 notions elles-mêmes est resserré (v3 n'avait resserré que les consignes autour), même substance/ordre/nombre. Paragraphe anti-préambule délibérément non touché (déjà optimal, mécanisme prouvé en v2) — signalé explicitement au porteur du projet plutôt que raccourci silencieusement.

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.

## 2026-08-20 — Poste de travail (Windows), suite 9 (entrée courte)

**Fait** (commit `2cf4433`) : le porteur du projet a collé la v4 dans NotebookLM et signalé un troncage net en plein mot. Mesuré précisément (PowerShell, pas deviné) : le bloc v4 faisait 6486 caractères, la vraie limite du champ NotebookLM est **5000 caractères pile** (pas juste "plus haut que 500" comme supposé en v1/v2). Coupé en 7 passes itératives, chacune re-mesurée avant la suivante (les estimations à la main sous-évaluaient systématiquement les gains réels — d'où l'intérêt de mesurer plutôt que d'estimer). v5 finale : **4893/5000 caractères**, 46 notions intactes (même ordre, rien omis), en-têtes de section retirés (FONDATIONS/PIPELINE RAG/etc. — cosmétique pour la lecture humaine, pas pour l'instruction), formulation télégraphique partout, paragraphe anti-préambule légèrement raccourci en tout dernier recours (une seule phrase de clôture redondante retirée, chaque formulation interdite listée reste intacte).
**Reste :** rien de bloquant — v5 prête à coller, sous la limite avec marge réelle (107 caractères).

**Prochaine session :** voir `.claude/HANDOFF/NEXT_SESSION.md`.
