# AGIRH V8 — Contexte projet

Assistant RH agentique (Onboarding/Offboarding), stage été 2026. Projet reconstruit de zéro le 2026-08-14 (V7→V8) — voir `docs/HISTORIQUE.md` pour le pourquoi.

## Rôle de chaque session : exécutant, pas décideur

Ce dépôt encode déjà toutes les décisions produit et techniques actées avec le porteur du projet (`docs/LOGIQUE_METIER.md`, `docs/STACK_TECHNIQUE.md`, `docs/ARCHITECTURE.md`). **Une session qui reprend ce projet — en particulier depuis un autre appareil — doit exécuter la suite du plan déjà acté, pas le réinventer ni improviser.**

- Ne jamais trancher seul(e) une question de logique métier, d'architecture, ou de choix technique qui n'est pas déjà répondue dans ces documents. Si une décision manque : l'ajouter à la section "Décisions en attente" de `.claude/HANDOFF/NEXT_SESSION.md` et **poser la question au porteur du projet** plutôt que de choisir à sa place. C'est le mode de collaboration établi depuis le début de ce projet — beaucoup de questions avant d'agir, jamais de décision produit unilatérale.
- Suivre l'ordre de construction déjà acté (`docs/LOGIQUE_METIER.md` §10, `docs/CHECKLIST.md`) plutôt que de réordonner les priorités de sa propre initiative.
- Ne pas réinterpréter ou "améliorer" silencieusement une décision déjà actée. Un désaccord ou une meilleure idée se signale explicitement au porteur du projet, ne s'applique pas unilatéralement.
- En cas de doute entre "je décide" et "je demande" : demander. Le coût d'une question est faible ; le coût d'une décision produit prise à la place du porteur du projet ne l'est pas.

## Documents canoniques (lire dans cet ordre)
1. **`.claude/HANDOFF/NEXT_SESSION.md`** — point de départ obligatoire : état courant, prochaine action concrète.
2. `docs/CHECKLIST.md` — suivi détaillé milestone par milestone (statuts en émojis).
3. `docs/LOGIQUE_METIER.md` — rôles, workflows, RBAC, garde-fous IA.
4. `docs/STACK_TECHNIQUE.md` — stack backend/frontend/données/IA.
5. `docs/ARCHITECTURE.md` — hexagonal, arborescence, diagrammes.
6. `.claude/context/PROJECT_STATE.md` — pointeur pour les agents custom (`.claude/agents/`).

Les autres documents de cadrage (`docs/HISTORIQUE.md`, `docs/SUJET_STAGE.md`) et la documentation complémentaire (`docs/notebooklm/`) vivent aussi sous `docs/` — seuls `CLAUDE.md` (chargé automatiquement par l'outillage) et `README.md` (convention GitHub) restent à la racine du dépôt.

## Protocole de continuité entre sessions (PC ↔ mobile)

Ce projet est travaillé depuis plusieurs appareils (poste de travail + Claude Code mobile). Chaque session doit repartir du bon état ET laisser une trace exploitable par la suivante.

**En début de session** : lire `.claude/HANDOFF/NEXT_SESSION.md` en premier. Il donne l'état courant et l'action suivante concrète — pas besoin de deviner ou de relire tout l'historique de conversation (qui n'existe pas d'une session à l'autre).

**En fin de session, ou après un changement significatif** (jalon terminé, décision produit/technique actée, bug important corrigé) :
1. Réécrire `.claude/HANDOFF/NEXT_SESSION.md` — c'est un instantané de l'état courant, pas un journal (ne pas y accumuler l'historique).
2. Ajouter une entrée en fin de `.claude/HANDOFF/LOG.md` (date, appareil/session, ce qui a été fait, ce qui reste) — ne jamais modifier une entrée existante.
3. Mettre à jour `docs/CHECKLIST.md` si un milestone a changé de statut.
4. `git add`, `git commit`, `git push origin master`.

**Ne pas sauter cette étape**, même pour une session courte — c'est le seul mécanisme qui permet à l'autre appareil de savoir ce qui a été fait. Sans push, le travail reste invisible ailleurs.

## Granularité du travail — protocole tout-ou-rien

Les sessions (notamment mobile) peuvent planter en cours de route. Pour qu'un plantage ne laisse jamais un état ambigu pour la session suivante :

- **Découper le travail en incréments indépendamment vérifiables** (un jalon, une fonctionnalité testée, un fix) plutôt que d'accumuler beaucoup de changements avant un seul gros commit final.
- **Après CHAQUE incrément vérifié** (build vert + tests verts, ou vérification manuelle explicite) : committer, pousser (`git push origin master`), et ajouter l'entrée à `.claude/HANDOFF/LOG.md` **immédiatement** — ne pas attendre la fin de la session pour tout regrouper en un seul checkpoint.
- **Ne jamais logger comme "fait" un travail non vérifié.** Si la vérification échoue ou que la session s'arrête avant de vérifier, une éventuelle entrée de log doit dire "en cours" / "interrompu", jamais "fait".
- **Marqueur de travail en cours** : avant de commencer un incrément qui prendra plus de quelques minutes, créer `.claude/HANDOFF/.in_progress` (une ligne texte décrivant ce qui est en cours). Le supprimer juste après le commit+push réussi de cet incrément.
- **Si une nouvelle session trouve `.claude/HANDOFF/.in_progress` présent** : la session précédente a probablement planté en cours de route. Vérifier `git status` et `git diff` avant de faire confiance à quoi que ce soit de non commité — décider explicitement de garder, corriger, ou annuler ce travail interrompu, puis supprimer le marqueur une fois la situation clarifiée. Ne jamais ignorer ce fichier silencieusement.

Un commit poussé est la seule preuve de travail qui compte réellement. `.claude/HANDOFF/NEXT_SESSION.md` et `.claude/HANDOFF/LOG.md` ne sont que des résumés lisibles de ce que l'historique git contient déjà — en cas de doute ou de contradiction, l'historique git fait foi.

## Commandes de développement
```
dotnet build Agirh.sln -c Release
dotnet test Agirh.sln -c Release
```

Base de données locale : conteneur Docker `arhia-sql` (SQL Server, port 1433). Démarrer avec `docker start arhia-sql` si arrêté — **ne pas le recréer**, il contient déjà le schéma V8 à jour. Identifiants et clé JWT dans `src/Agirh.Api/appsettings.Development.json` (non commité — voir `appsettings.json.example` pour la structure attendue).
