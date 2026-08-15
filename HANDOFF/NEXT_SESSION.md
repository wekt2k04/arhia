# Reprise de session — AGIRH V8

*Dernière mise à jour : 2026-08-15, poste de travail (Windows). Ce fichier est **réécrit** à chaque checkpoint (pas un journal) — pour l'historique complet, voir `HANDOFF/LOG.md`.*

## En une phrase
Socle métier, authentification, et **pipeline RAG complet (4 phases)** sont fonctionnels et vérifiés contre de vraies infrastructures (SQL Server, Qdrant, modèles ONNX réels — pas de mocks). Milestone 7 terminé. Prochaine étape : milestone 8 (orchestration conversationnelle) ou milestone 6 (frontend).

## Ce qui marche déjà (vérifié, pas juste écrit)
- `dotnet build Agirh.sln -c Release` → 0 erreur, 0 warning
- `dotnet test Agirh.sln -c Release` → **142/142 verts**, dont des tests d'intégration réels (pas mockés) contre ONNX Runtime et Qdrant
- SQL Server réel (conteneur `agirh-sql`, port 1433), schéma V8 appliqué
- Auth testée en HTTP réel (register/login/me/élévation de rôle)
- **Pipeline RAG 4 phases vérifié de bout en bout sur un vrai document du corpus** : chunking structurel (`MarkdownChunker`) → embedding ONNX multilingue 768d (`OnnxEmbeddingAdapter`, `Xenova/paraphrase-multilingual-mpnet-base-v2`) → indexation/recherche Qdrant (`QdrantVectorSearchAdapter`) → reranking cross-encoder ONNX (`OnnxRerankerAdapter`, `onnx-community/bge-reranker-v2-m3-ONNX`) — le test capstone confirme que la bonne réponse ressort en tête sur une vraie requête

## Prochaine action concrète
**Pas encore décidé avec le porteur du projet.** Deux options, en attente de son choix :
1. **Milestone 8 — Orchestration conversationnelle** (Router + Generator via Ollama) : câble le pipeline RAG (déjà prêt) dans un vrai flux de chat. Nécessite de créer un use case Core (`RechercherDansCorpusUseCase` ou équivalent) qui orchestre embedding→recherche→reranking derrière un port, puis le Router/Generator par-dessus.
2. **Milestone 6 — Frontend** (page de garde + chat + notifications SSE, Next.js/BFF) — n'a de sens que si le chat a quelque chose de réel à consommer, donc probablement après le 8.

**Recommandation implicite du travail déjà fait** : le 8 est la suite naturelle puisque le RAG est prêt et n'est câblé nulle part encore — mais ne pas trancher sans demander, cohérent avec le protocole établi (`CLAUDE.md`).

## Comment reprendre concrètement
1. Lire ce fichier en entier, puis `CHECKLIST.md` pour le détail milestone par milestone.
2. Vérifier l'état réel avant de supposer quoi que ce soit : `git log --oneline -5`, `git status`. Un autre appareil a pu avancer depuis la rédaction de ce fichier.
2bis. **Vérifier si `HANDOFF/.in_progress` existe.** Si oui, une session précédente a probablement planté en plein travail — lire ce fichier, examiner `git status`/`git diff`, décider de garder/corriger/annuler avant de continuer (protocole détaillé dans `CLAUDE.md`).
3. Infrastructure locale nécessaire, à démarrer si arrêtée : `docker start agirh-sql` (SQL Server, **ne pas recréer**) et `docker start agirh-qdrant` (Qdrant, collection `agirh-corpus` déjà créée si des tests ont tourné). Modèles ONNX : `powershell -ExecutionPolicy Bypass -File scripts/download-models.ps1` (idempotent, ~850 Mo, jamais commité — **attention aux téléchargements en arrière-plan, voir piège ci-dessous**).
4. Avant de coder une nouvelle logique métier ou un choix technique : relire `LOGIQUE_METIER.md` / `STACK_TECHNIQUE.md` / `ARCHITECTURE.md` si la tâche touche à une décision déjà actée — ne pas re-décider en silence.
5. **À la fin de la session (ou après un jalon terminé)** : mettre à jour ce fichier + `HANDOFF/LOG.md` + `CHECKLIST.md`, puis `git commit` + `git push origin master`. Protocole détaillé dans `CLAUDE.md`.

## Décisions en attente (à trancher avec le porteur du projet)
- Choix entre milestone 8 (orchestration) et milestone 6 (frontend) comme prochaine étape.
- Temps restant sur le stage et livrables attendus (rapport, soutenance, dépôt, démo live) — jamais communiqué.
- Comportements précis des 3 cas particuliers (`LOGIQUE_METIER.md` §8 : mutation inter-pôle, annulation/suspension, pôle vacant).
- Contenu exact du jeu de questions/réponses gold (le corpus documentaire lui-même est fait — `corpus/`, 6 documents).
- Noms définitifs des ~5 pôles/départements.

## Pièges techniques rencontrés (à ne pas refaire)
- **EF Core** : une navigation de collection *owned* (`OwnsMany`) ne peut jamais être un paramètre de constructeur — EF le rejette au démarrage. Détail dans `.claude/agents/hexagonal-architect.md`.
- **Tokenisation XLM-RoBERTa** : `Microsoft.ML.Tokenizers.SentencePieceTokenizer` retourne les identifiants en espace SentencePiece brut, PAS en espace Hugging Face attendu par les poids ONNX. Toujours vérifier empiriquement ce genre d'hypothèse — voir `XlmRobertaTokenizer.cs`.
- **Modèles ONNX communautaires** : `onnx-community/bge-reranker-v2-m3-ONNX` n'a pas de `sentencepiece.bpe.model` propre — récupéré depuis `BAAI/bge-reranker-v2-m3` (même vocabulaire).
- **Reranking cross-encoder** : format de paire RoBERTa = `<s> requête </s></s> document </s>` (double séparateur EOS, convention de la famille RoBERTa, pas un choix du modèle).
- **Téléchargements en arrière-plan** : un `ScheduleWakeup({stop:true})` a tué par effet de bord une tâche `run_in_background` non liée (le téléchargement du modèle de reranking) — ne pas utiliser `ScheduleWakeup` pour attendre une tâche déjà suivie en arrière-plan, la notification arrive automatiquement à la fin de cette tâche.
