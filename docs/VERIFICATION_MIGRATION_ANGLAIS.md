# Vérifier la migration français → anglais — checklist pratique

*Steps concrets pour confirmer, par vous-même et visuellement, que la migration du vocabulaire
métier (français → anglais) est bien terminée et que rien n'est cassé. Chaque étape dit quoi taper,
quoi regarder, et à quoi ressemble un résultat correct. Compter ~15-20 min pour tout faire.*

## 0. Ce que cette migration a changé (et n'a PAS changé)

- **Changé** : tous les noms de classes/méthodes/propriétés métier en C#, les routes HTTP, le
  schéma de base de données (tables/colonnes), les clés Qdrant, les clés de configuration.
- **Volontairement inchangé** : tout le texte visible par un utilisateur final (interface, réponses
  du chat, contenu du corpus RAG) reste en français. Le prompt système du routeur IA reste en
  français (contrat calibré avec le modèle). Les valeurs `CDI`/`CDD`/`Stage`/`Alternance` et les
  profils `Maison`/`Entreprise` restent tels quels — décisions actées, pas des oublis.
- **Autrement dit** : si vous ouvrez l'app dans un navigateur, **vous ne devriez rien voir de
  différent**. La migration se vérifie dans le code et les outils techniques, pas à l'écran.

## 1. Le code compile et les tests passent

```bash
dotnet build Agirh.sln -c Release
dotnet test Agirh.sln -c Release --filter "Category!=Evaluation"
```

- **Attendu** : `0 Warning(s)`, `0 Error(s)` pour le build ; `211/211` verts pour les tests.
- **Si 1 test échoue sur `OllamaRouterAdapterTests`** (question "Mon dossier est-il clôturé ?") :
  c'est un flake connu (le routeur IA se trompe ~27% du temps, non-déterminisme du modèle, documenté
  depuis plusieurs sessions) — pas une régression de la migration. Relancez juste ce test :
  ```bash
  dotnet test Agirh.sln -c Release --filter "FullyQualifiedName~OllamaRouterAdapterTests"
  ```
  S'il passe au second essai, tout va bien.

## 2. Aucun identifiant métier français ne traîne dans le code

```bash
grep -rEn "Collaborateur|CompteUtilisateur|PoleScopeGuard|AccesRefuse|ExecuterAsync" src/ tests/ frontend/app frontend/lib frontend/components --include="*.cs" --include="*.ts" --include="*.tsx"
```

- **Attendu** : aucune ligne retournée (commande muette = vide = correct).
- Si quelque chose ressort : vérifiez que ce n'est pas une des exceptions volontaires (voir §0) avant
  de le signaler comme un oubli.

## 3. Le schéma de base de données est en anglais

```bash
docker start agirh-sql   # si pas déjà démarré
MSYS_NO_PATHCONV=1 docker exec agirh-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<mot de passe de appsettings.Development.json>' -C -Q "SELECT TABLE_NAME FROM AgirhDb.INFORMATION_SCHEMA.TABLES ORDER BY TABLE_NAME"
```

- **Attendu**, exactement ces 8 tables : `ChecklistItemStatuses`, `Departments`, `Employees`,
  `TemplateItems`, `TemplateSections`, `UserAccounts`, `WorkflowInstances`, `WorkflowTemplates`.
- Aucune table `Collaborateurs`/`Poles`/`ComptesUtilisateurs` ne doit apparaître.

## 4. L'application tourne et se comporte normalement (test visuel)

1. Démarrer l'infra si besoin : `docker start agirh-sql agirh-qdrant`, `ollama serve` (natif),
   puis `cd src/Agirh.Api && dotnet run --launch-profile Maison` (port 5080) et
   `cd frontend && npm run dev` (port 3000).
2. Ouvrir `http://localhost:3000` dans un navigateur.
3. **Créer un compte** (page inscription) → doit réussir, rôle par défaut = Employee (invisible à
   l'écran, mais vérifiable en base : `SELECT Role FROM UserAccounts WHERE Email = '...'` → `0`).
4. **Se connecter** avec ce compte → redirection vers `/chat`, tout le texte affiché doit être en
   **français** (labels, placeholders, messages).
5. **Poser une question dans le chat** (ex. "Quelle est la politique de mot de passe ?") → la
   réponse doit arriver progressivement (streaming), en français, avec des sources affichées à la
   fin. Si la réponse dit "je n'ai pas trouvé cette information" à une question qui devrait avoir une
   réponse dans le corpus, voir §5 (Qdrant a peut-être besoin d'une réindexation).
6. Ouvrir les **outils développeur du navigateur (F12) → Network**, cliquer sur la requête vers
   `/api/chat/ask` (pas `/api/chat/demander`) → confirme que la route BFF est bien en anglais.

## 5. Le pipeline RAG (Qdrant) est à jour

Si le chat répond "je n'ai pas trouvé cette information" à des questions qui devraient avoir une
réponse dans le corpus, la collection Qdrant contient probablement encore l'ancien schéma de clés
(`cheminTitres`/`contenu` au lieu de `titlePath`/`content`). Pour resynchroniser :

1. Se connecter avec un compte **Admin/Qualité** (promotion manuelle en SQL si besoin :
   `UPDATE UserAccounts SET Role = 2 WHERE Email = '...'`, puis se reconnecter pour rafraîchir le
   JWT).
2. Appeler l'endpoint de réindexation (remplacer `<TOKEN>` par le JWT du cookie, ou passer par
   l'interface si un bouton existe) :
   ```bash
   curl -X POST http://localhost:5080/api/admin/reindex-corpus -H "Authorization: Bearer <TOKEN>"
   ```
3. **Attendu** : `{"documentsRead":6,"chunksIndexed":72}` (ou un nombre proche selon le corpus
   actuel). Reposer la question du chat → devrait maintenant trouver la réponse.

## 6. La documentation cite du vrai code

Ouvrir n'importe quel document sous `docs/notebooklm/` ou `docs/APPRENTISSAGE/principal.md` et
comparer un extrait de code cité (avec son chemin de fichier) au fichier réel — les noms de
classes/méthodes doivent correspondre exactement à ce qui est dans `src/`. C'est plus une
vérification de confiance ponctuelle qu'une étape systématique : si un extrait ne correspond pas,
c'est un oubli à signaler.

## En cas de problème

- **Un identifiant français apparaît quelque part d'inattendu** → vérifier d'abord que ce n'est pas
  une des exceptions volontaires du §0, sinon c'est un oubli réel à corriger.
- **Le build ou les tests échouent autrement qu'au §1** → ne pas supposer que c'est lié à la
  migration ; vérifier `git log --oneline -5` pour confirmer qu'on est bien sur le dernier commit
  poussé, et consulter `.claude/HANDOFF/NEXT_SESSION.md` pour le contexte le plus récent.
