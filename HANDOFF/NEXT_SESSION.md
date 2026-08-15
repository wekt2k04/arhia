# Reprise de session — AGIRH V8

*Dernière mise à jour : 2026-08-15, poste de travail (Windows). Ce fichier est **réécrit** à chaque checkpoint (pas un journal) — pour l'historique complet, voir `HANDOFF/LOG.md`.*

## En une phrase
Socle métier, authentification, **pipeline RAG complet**, et **orchestration conversationnelle (Router+Generator)** sont fonctionnels et vérifiés en HTTP réel de bout en bout (SQL Server, Qdrant, Ollama, modèles ONNX réels — pas de mocks). Milestones 0-5, 7 et 8 terminés. Prochaine étape : à trancher avec le porteur du projet (milestone 6 frontend, ou autre).

## Ce qui marche déjà (vérifié, pas juste écrit)
- `dotnet build Agirh.sln -c Release` → 0 erreur, 0 warning
- `dotnet test Agirh.sln -c Release` → **158/158 verts**, dont des tests d'intégration réels (pas mockés) contre ONNX Runtime, Qdrant et Ollama
- SQL Server réel (conteneur `agirh-sql`, port 1433), schéma V8 appliqué
- Auth testée en HTTP réel (register/login/me/élévation de rôle)
- **Pipeline RAG 4 phases** vérifié de bout en bout sur un vrai document du corpus (chunking → embedding ONNX → Qdrant → reranking ONNX)
- **Chat conversationnel vérifié en HTTP réel de bout en bout**, les 3 branches :
  1. Question documentaire réelle ("Quelle est la politique de mot de passe de l'entreprise ?") → réponse **sourcée correctement** depuis `corpus/04_procedures_it_securite.md` (via `POST api/chat/demander`)
  2. Question hors-périmètre ("Quelle est la capitale de l'Australie ?") → refus poli, aucun appel au RAG
  3. Question de statut sans fiche collaborateur → message gracieux "dossier introuvable" (comportement attendu, aucune fiche seedée dans cette base V8 fraîche)
- Réindexation du corpus réel vérifiée : `POST api/admin/reindexer-corpus` (RBAC AdminQualite) → 6 documents, 72 chunks indexés dans Qdrant sous leurs vrais noms

## Prochaine action concrète
**Pas encore décidé avec le porteur du projet.** À l'issue du milestone 8, les options restantes :
1. **Milestone 6 — Frontend** (page de garde + chat + notifications SSE, Next.js/BFF) — le chat a maintenant un vrai backend à consommer, donc ça a du sens de l'attaquer.
2. **Milestone 9 — Jeu de Q/R gold formel** (~30-50 paires question/réponse/source attendue) pour mesurer précisément retrieval + fidélité et calibrer le seuil de pertinence du reranking (actuellement 0.01, provisoire).
3. Exposer en HTTP les endpoints Collaborateur/Workflow/Template (use cases Core prêts et testés depuis le milestone 3, jamais exposés via Controller — seul Auth + Chat + Admin le sont).

**Ne pas trancher sans demander** — cohérent avec le protocole établi (`CLAUDE.md`, "exécutant, pas décideur").

## Comment reprendre concrètement
1. Lire ce fichier en entier, puis `CHECKLIST.md` pour le détail milestone par milestone.
2. Vérifier l'état réel avant de supposer quoi que ce soit : `git log --oneline -5`, `git status`. Un autre appareil a pu avancer depuis la rédaction de ce fichier.
2bis. **Vérifier si `HANDOFF/.in_progress` existe.** Si oui, une session précédente a probablement planté en plein travail — lire ce fichier, examiner `git status`/`git diff`, décider de garder/corriger/annuler avant de continuer (protocole détaillé dans `CLAUDE.md`).
3. Infrastructure locale nécessaire, à démarrer si arrêtée : `docker start agirh-sql` (SQL Server, **ne pas recréer**) et `docker start agirh-qdrant` (Qdrant, collection déjà peuplée avec le vrai corpus si le milestone 8 a tourné au moins une fois). `ollama serve` doit tourner avec `phi4-mini:3.8b` disponible (`ollama pull phi4-mini:3.8b` si absent). Modèles ONNX : `powershell -ExecutionPolicy Bypass -File scripts/download-models.ps1` (idempotent, ~850 Mo, jamais commité).
4. Avant de coder une nouvelle logique métier ou un choix technique : relire `LOGIQUE_METIER.md` / `STACK_TECHNIQUE.md` / `ARCHITECTURE.md` si la tâche touche à une décision déjà actée — ne pas re-décider en silence.
5. Pour tester le chat manuellement en HTTP depuis ce poste (Git Bash/Windows) : **passer par un fichier JSON (`curl --data-binary @fichier.json`) plutôt qu'une chaîne shell** dès que la question contient un caractère accentué — la ligne de commande Windows corrompt l'UTF-8 des accents, ce n'est pas un bug applicatif (piège rencontré et confirmé ce checkpoint).
6. Aucun compte AdminQualite n'existe par défaut dans une base fraîche (le premier compte doit être promu manuellement en SQL — `UPDATE ComptesUtilisateurs SET Role = 'AdminQualite' WHERE Email = '...'` — puisque `elever-role` exige déjà un acteur Admin/Qualité). Deux comptes de test existent dans la base dev actuelle : `chattest@agirh.test` et `admintest@agirh.test`, tous deux promus AdminQualite.
7. **À la fin de la session (ou après un jalon terminé)** : mettre à jour ce fichier + `HANDOFF/LOG.md` + `CHECKLIST.md`, puis `git commit` + `git push origin master`. Protocole détaillé dans `CLAUDE.md`.

## Décisions en attente (à trancher avec le porteur du projet)
- Prochaine étape : milestone 6 (frontend) vs milestone 9 (Q/R gold) vs endpoints Api restants.
- Temps restant sur le stage et livrables attendus (rapport, soutenance, dépôt, démo live) — jamais communiqué.
- Comportements précis des 3 cas particuliers (`LOGIQUE_METIER.md` §8 : mutation inter-pôle, annulation/suspension, pôle vacant).
- Contenu exact du jeu de questions/réponses gold (le corpus documentaire lui-même est fait — `corpus/`, 6 documents, 72 chunks indexés).
- Noms définitifs des ~5 pôles/départements.

## Pièges techniques rencontrés (à ne pas refaire)
- **EF Core** : une navigation de collection *owned* (`OwnsMany`) ne peut jamais être un paramètre de constructeur — EF le rejette au démarrage. Détail dans `.claude/agents/hexagonal-architect.md`.
- **Tokenisation XLM-RoBERTa** : `Microsoft.ML.Tokenizers.SentencePieceTokenizer` retourne les identifiants en espace SentencePiece brut, PAS en espace Hugging Face attendu par les poids ONNX. Toujours vérifier empiriquement ce genre d'hypothèse — voir `XlmRobertaTokenizer.cs`.
- **Modèles ONNX communautaires** : `onnx-community/bge-reranker-v2-m3-ONNX` n'a pas de `sentencepiece.bpe.model` propre — récupéré depuis `BAAI/bge-reranker-v2-m3` (même vocabulaire).
- **Reranking cross-encoder** : format de paire RoBERTa = `<s> requête </s></s> document </s>` (double séparateur EOS, convention de la famille RoBERTa, pas un choix du modèle).
- **Téléchargements en arrière-plan** : un `ScheduleWakeup({stop:true})` a tué par effet de bord une tâche `run_in_background` non liée — ne pas utiliser `ScheduleWakeup` pour attendre une tâche déjà suivie en arrière-plan, la notification arrive automatiquement à la fin de cette tâche.
- **Ollama** : `gemma4:12b` est trop lent sur cette machine (>2min sans réponse, pas de GPU) — Router et Generator utilisent tous deux `phi4-mini:3.8b`. L'API Ollama attend du JSON en **minuscules** (`model`, `system`, `prompt`, `stream`) — toujours passer `JsonSerializerOptions(JsonSerializerDefaults.Web)` sur les clients HTTP typés qui l'appellent.
- **curl / accents sous Windows Git Bash** : une chaîne JSON passée en argument shell avec un caractère accentué (`Où`, `é`...) peut être corrompue par l'encodage de la console avant même d'atteindre curl, provoquant un faux 400 "JSON invalide" côté Api alors que le code est correct. Toujours passer par un fichier (`--data-binary @fichier.json`) pour tester manuellement du texte accentué.
- **Bootstrap du premier compte Admin/Qualité** : aucun mécanisme applicatif ne permet de créer le tout premier compte Admin (par design — `elever-role` exige déjà un acteur Admin/Qualité). En dev, promotion manuelle via `UPDATE ComptesUtilisateurs SET Role = 'AdminQualite' ...` directement en SQL. Pas un bug — juste un point à retenir pour toute démo/déploiement (script de seed à prévoir avant la soutenance).
