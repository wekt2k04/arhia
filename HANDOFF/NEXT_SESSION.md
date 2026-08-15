# Reprise de session — AGIRH V8

*Dernière mise à jour : 2026-08-15, poste de travail (Windows). Ce fichier est **réécrit** à chaque checkpoint (pas un journal) — pour l'historique complet, voir `HANDOFF/LOG.md`.*

## En une phrase
Le socle métier (Domain/Core) et l'authentification (Infrastructure/Api) sont fonctionnels, testés (121/121), et vérifiés contre une vraie base SQL Server. Prochaine étape à trancher : frontend ou pipeline RAG.

## Ce qui marche déjà (vérifié, pas juste écrit)
- `dotnet build Agirh.sln -c Release` → 0 erreur, 0 warning
- `dotnet test Agirh.sln -c Release` → **121/121 verts**
- Base SQL Server réelle (conteneur Docker `agirh-sql`, port 1433) avec le schéma V8 appliqué (migration `InitialCreate`)
- Auth testée en HTTP réel : `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` (JWT), `POST /api/auth/elever-role` (register→200, login→200, me→200, mauvais mot de passe→401)

## Prochaine action concrète
**Pas encore décidé avec le porteur du projet.** Deux options proposées en fin de dernière session, en attente de son choix :
1. **Frontend** (page de garde publique + chat + barre de notifications SSE, Next.js, BFF) — rend le projet démontrable visuellement. Milestone 6 de `CHECKLIST.md`.
2. **Pipeline RAG** (chunking structurel → embedding ONNX multilingue → Qdrant → reranking cross-encoder ONNX) — la partie la plus risquée techniquement, priorité absolue si le temps manque en fin de stage (`LOGIQUE_METIER.md` §10). Milestone 7.

Si la session reprend sans réponse du porteur de projet sur ce choix : **demander avant de commencer**, ne pas trancher seul (cohérent avec le mode de collaboration établi tout au long du projet — beaucoup de questions avant d'agir).

## Comment reprendre concrètement
1. Lire ce fichier en entier, puis `CHECKLIST.md` pour le détail milestone par milestone.
2. Vérifier l'état réel avant de supposer quoi que ce soit : `git log --oneline -5`, `git status`. Un autre appareil a pu avancer depuis la rédaction de ce fichier.
2bis. **Vérifier si `HANDOFF/.in_progress` existe.** Si oui, une session précédente a probablement planté en plein travail — lire ce fichier, examiner `git status`/`git diff`, décider de garder/corriger/annuler avant de continuer (protocole détaillé dans `CLAUDE.md`).
3. Si la tâche touche à SQL Server : le conteneur `agirh-sql` doit tourner (`docker start agirh-sql`) — il contient déjà le schéma V8, **ne pas le recréer** ni le droper.
4. Avant de coder une nouvelle logique métier ou un choix technique : relire `LOGIQUE_METIER.md` / `STACK_TECHNIQUE.md` / `ARCHITECTURE.md` si la tâche touche à une décision déjà actée — ne pas re-décider en silence.
5. **À la fin de la session (ou après un jalon terminé)** : mettre à jour ce fichier + `HANDOFF/LOG.md` + `CHECKLIST.md`, puis `git commit` + `git push origin master`. Protocole détaillé dans `CLAUDE.md`.

## Décisions en attente (à trancher avec le porteur du projet)
- Temps restant sur le stage et livrables attendus (rapport, soutenance, dépôt, démo live) — jamais communiqué.
- Comportements précis des 3 cas particuliers (`LOGIQUE_METIER.md` §8 : mutation inter-pôle, annulation/suspension, pôle vacant).
- Contenu exact du corpus RAG et du jeu de questions/réponses gold.
- Noms définitifs des ~5 pôles/départements.

## Piège technique à ne pas refaire
Une navigation de collection *owned* EF Core (`OwnsMany`, ex. `WorkflowTemplate.Sections`) ne peut jamais être un paramètre de constructeur — EF le rejette au démarrage. Détail complet dans `.claude/agents/hexagonal-architect.md` (section dédiée) et `.claude/context/PROJECT_STATE.md`.
