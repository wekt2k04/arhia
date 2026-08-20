# Rapport de peuplement — base de développement (AgirhDb)

*Généré le 2026-08-20. Décrit l'état du conteneur `agirh-sql` après peuplement complet — toutes
les tables métier ont désormais au moins une ligne. Données brutes complètes (mêmes informations,
format machine) : `donnees.json` dans ce même dossier.*

**Méthode** : tout a été créé via les vrais endpoints HTTP de l'Api (`register` → `elevate-role` →
`employees` → `templates` (propose/verify/approve) → `workflows` (instantiate/check/close/archive)),
jamais par insertion SQL directe — sauf la table `Departments`, qui n'a aucun endpoint de création
dans le code actuel. Chaque compte et chaque fiche a donc traversé le RBAC (`RbacMatrix`) et le
`DepartmentScopeGuard` exactement comme en usage réel.

## 1. Tables — avant / après

| Table | Avant | Après |
|---|---|---|
| Departments | 0 | **5** |
| UserAccounts | 1 | **9** (6 RH + 3 Admin/Qualité) |
| Employees | 0 | **25** |
| WorkflowTemplates | 0 | **2** (Onboarding + Offboarding, tous deux `Approved`) |
| TemplateSections | 0 | **11** |
| TemplateItems | 0 | **31** |
| WorkflowInstances | 0 | **28** (25 Onboarding + 3 Offboarding) |
| ChecklistItemStatuses | 0 | **565** |

Toutes les tables métier ont désormais des entrées (`__EFMigrationsHistory` est une table système
EF Core, pas une table métier — non concernée).

## 2. Infrastructure & migrations

- **Migrations** : une seule migration existe dans le projet, `20260818112315_InitialCreate`,
  déjà appliquée sur `agirh-sql` (vérifié dans `__EFMigrationsHistory`, confirmé par
  `dotnet ef migrations list`) — rien en attente.
- **⚠️ Risque identifié, non corrigé** : le conteneur `agirh-sql` n'a **aucun volume Docker
  attaché** (`docker inspect agirh-sql --format '{{json .Mounts}}'` → `[]`). Un `docker stop`/
  `start`/`restart`, ou un redémarrage machine, ne perd rien (la couche inscriptible du conteneur
  persiste sur disque). Le vrai risque est un `docker rm agirh-sql` (volontaire ou accidentel via
  un `docker compose up` mal aiguillé) : tout serait perdu sans sauvegarde. Migrer vers un volume
  nommé exige de recréer le conteneur — décision à prendre avec le porteur du projet, pas prise
  ici unilatéralement.

## 3. Comptes créés

Mot de passe commun à tous les comptes de ce lot (fixture de dev, usage local uniquement) :
**`AgirhDev2026!`**

### Admin / Qualité — portée globale

| Email | Compte Id | Origine |
|---|---|---|
| `wilfried@agirh.test` | `c38a025c…a42f2c` | session précédente |
| `admin2@agirh.test` | `7143ab94…74c028` | ce lot |
| `e2etest@agirh.test` | — | préexistant, mot de passe inconnu |

### RH — portée limitée à leur pôle

| Email | Pôle | Compte Id |
|---|---|---|
| `hr.direction-technique.1@agirh.test` | Direction Technique | `fc58adf4…58048531` |
| `hr.direction-technique.2@agirh.test` | Direction Technique | `63c15096…06950e26` |
| `hr.ressources-humaines@agirh.test` | Ressources Humaines | `c8d04734…00297890b` |
| `hr.finance-comptabilite@agirh.test` | Finance et Comptabilite | `fa03682c…1dc1053940` |
| `hr.commercial-marketing@agirh.test` | Commercial et Marketing | `456be0e5…fb72e261b3` |
| `hr.support-client@agirh.test` | Support Client | `a3cd4fe8…9ef22bc02e33` |

Direction Technique porte volontairement 2 comptes RH (demandé explicitement) — rien dans
`DepartmentScopeGuard` n'empêche plusieurs RH sur un même pôle ; vérifié en créant les 5 fiches du
pôle en alternant les deux comptes (3 via le premier, 2 via le second), aucune erreur.

## 4. Pôles/départements (noms **provisoires**)

Le nom définitif des ~5 pôles reste une décision ouverte du projet (`.claude/HANDOFF/NEXT_SESSION.md`,
section "Décisions en attente") — non tranchée ici, juste choisi pour peupler la base :

| Pôle | Id | RH | Collaborateurs |
|---|---|---|---|
| Direction Technique | `3253bfa9-761b-409d-b4d5-4f7666924189` | 2 | 5 |
| Ressources Humaines | `91a06825-0b74-40e3-93d2-bc58605ef650` | 1 | 5 |
| Finance et Comptabilite | `cbce3bea-5c19-4444-9836-5a355cd67b6f` | 1 | 5 |
| Commercial et Marketing | `35acc47e-127f-4835-b6a6-520ab7f99d4e` | 1 | 5 |
| Support Client | `3faef131-285d-4452-92b7-a5a2c1169d0f` | 1 | 5 |

## 5. Templates — contenu réel `SMSI.ENR.10-1`/`10-2`

Contenu repris **verbatim** de `docs/LOGIQUE_METIER.md` §3-4 (source de vérité métier), pas
inventé. Les deux ont traversé le circuit complet Rédacteur → Vérificateur → Approbateur et sont
au statut `Approved` :

- **Onboarding** (`SMSI.ENR.10-1 v1`, id `e7c6b674-ba18-4f1c-9934-7f606b7e7603`) — 6 sections, 22
  items : IT (6) · Messagerie (2) · Active Directory (6) · Pointage (1) · Sensibilisation SI (3) ·
  RH (4, dont 2 exclus pour `Stage` : *Compte SELFRH créé*, *Processus disciplinaire signé*).
- **Offboarding** (`SMSI.ENR.10-2 v1`, id `baf91c0a-7692-4eb0-910f-58b8d2416711`) — 5 sections, 9
  items : Poste (3) · Messagerie (1) · Active Directory & Accès (2) · Pointage (1) · RH (2, dont 1
  exclu pour `Stage` : *Compte SELFRH supprimé*).
- Rédacteur : `hr.ressources-humaines@agirh.test` · Vérificateur : `wilfried@agirh.test` ·
  Approbateur : `admin2@agirh.test`.

**Preuve que la résolution Poste × Pôle × Contrat fonctionne réellement** : les 5 collaborateurs en
contrat `Stage` reçoivent bien **20 items** au lieu de 22 en Onboarding (et 8 au lieu de 9 en
Offboarding) — vérifié en base, pas juste supposé. `WorkflowTemplate.ResolveApplicableItems` exclut
correctement les 2 items marqués `ApplicableContractTypes` sans `Stage`.

## 6. Collaborateurs & workflows

25 collaborateurs (5 par pôle), les 4 types de contrat représentés dans chaque pôle. Chacun a une
instance Onboarding (`WorkflowInstance`), avec une répartition volontairement variée d'avancement :

| Cas | Collaborateurs concernés | Détail |
|---|---|---|
| Clôturé | EMP-0011 (Emma Lefevre) | 22/22 items cochés, `Close()` appelé |
| Archivé | EMP-0001 (Marie Dubois) | 22/22 cochés (1 marqué `Failed` avec commentaire), `Close()` puis `Archive()` |
| En cours, partiel | EMP-0002, 0006, 0012, 0016, 0017, 0021, 0022 | 2 items cochés `Done` chacun |
| En cours, frais | les 16 autres | 0 item coché, tel qu'instancié |

3 instances **Offboarding** créées en plus (démontrent que ce second template fonctionne aussi) :
EMP-0003 et EMP-0023 (`Stage`, 8/8 items — exclusion SELFRH confirmée), EMP-0009 (`Alternance`,
9/9 items). Toutes trois fraîches (`InProgress`), aucun item coché.

Détail complet des 25 fiches (matricule, poste, contrat, date d'entrée, id d'instance) :
`donnees.json`.

## 7. Vérification du workflow de compte

4 cas rejoués en HTTP réel contre l'Api en cours d'exécution, chacun teste un garde-fou codé, pas
supposé :

| Cas | Attendu | Obtenu |
|---|---|---|
| Inscription sur un email déjà utilisé | 409 | 409 ✓ |
| Mot de passe < 8 caractères | 400 | 400 ✓ |
| Connexion avec mauvais mot de passe | 401 | 401 ✓ |
| RH crée un collaborateur hors de son pôle (`DepartmentScopeGuard`) | 403 | 403 ✓ |

## 8. Nettoyage de la racine du dépôt (même session)

Deux fichiers de log LaTeX traînaient à la racine (`pdflatex_run2.log`, `texput.log`, sous-produits
d'une compilation lancée depuis la racine plutôt que depuis `docs/rapport_avancement/`) — déplacés
dans `docs/rapport_avancement/` (là où vivent déjà `rapport.tex`/`.pdf` et leurs diagrammes). Règle
`/*.log` ajoutée à `.gitignore` pour que ça ne revienne pas se loger à la racine. Rien d'autre à la
racine n'était mal placé — le reste (`.env`, `.gitignore`, `Agirh.sln`, `docker-compose.yml`,
`README.md`, `CLAUDE.md`, les dossiers `src/frontend/docs/rag/tests/.claude/.github/.vscode`) est
soit une convention d'outillage qui exige d'être à la racine, soit déjà documenté comme devant y
rester (`CLAUDE.md` lui-même).
