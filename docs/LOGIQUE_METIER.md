# Logique métier — arhia V8 (Onboarding / Offboarding)

*Document de cadrage issu d'une série de questions/réponses avec le porteur du projet. Sert de référence pour la conception technique (data model, workflow engine, RBAC, agent IA). Toute section marquée **"à valider"** n'a pas encore de décision explicite — proposition à confirmer avant implémentation.*

## 0. Périmètre

Le projet se limite strictement à l'**onboarding** et l'**offboarding** des collaborateurs (`SUJET_STAGE.md`). Pas de congés/CET/paie/avance sur salaire — ce périmètre avait dérivé en V7 et a été retranché lors de la remise à zéro (`HISTORIQUE.md`).

Source de vérité métier : les deux checklists qualité réelles de l'entreprise (`SMSI.ENR.10-1` Onboarding et `SMSI.ENR.10-2` Off-boarding, format ISO 27001 / SMSI). Les noms de personnes réels qui y figurent (rédacteur/vérificateur/approbateur du template T0) sont **anonymisés** dans tout artefact du projet — remplacés par des rôles génériques.

## 1. Rôles & organisation

**3 rôles**, pas plus :

| Rôle | Description | Volume |
|---|---|---|
| **Employee** | Consulte sa propre checklist, pose des questions à l'agent. Rôle par défaut à l'auto-inscription. | Majorité des comptes |
| **RH** | Un RH par pôle (département). Crée les fiches de ses collaborateurs assignés, coche les items RH, joue le rôle de **Rédacteur** dans le circuit de validation des templates, et sert de point de contact managérial (alertes). Un RH peut cumuler un tag "spécialisé IT" pour traiter les items techniques de son pôle. | ~5 comptes (1 par pôle) |
| **Admin/Qualité** | Joue **Vérificateur** + **Approbateur** dans le circuit de validation des templates. Peut élever le rôle de n'importe quel compte (simuler une promotion, ex: Employee → RH). | 2 comptes |

**Pas de rôle IT séparé** — le travail IT est dispatché chez le RH "spécialisé IT" de chaque pôle, pas une file d'attente ni un compte à part.

**Manager** : pas de rôle dédié. Le RH du pôle assume ce rôle de contact managérial pour les alertes (le sujet demande que "manager" soit alerté — c'est le RH référent qui porte cette responsabilité ici).

### Structure organisationnelle

~5 pôles = départements réels de l'entreprise, noms génériques inspirés de la structure réelle (anonymisée), chacun avec 3 à 5 collaborateurs. 1 seul RH par pôle.

### Auto-inscription

Un compte créé en self-service démarre toujours en rôle **Employee**. Seul un Admin/Qualité peut l'élever.

## 2. Déclenchement des processus

**Saisie manuelle par le RH du pôle concerné.** Pas d'import SIRH, pas de planification automatique par date, pas d'auto-enregistrement du collaborateur. La plateforme a vocation à **remplacer entièrement** le travail actuellement fait à la main (les 2 checklists Excel/Word réelles).

## 3. Checklist Onboarding — contenu de référence

Dérivé du document réel `SMSI.ENR.10-1`. Fiche collaborateur : Nom, Matricule, Poste, Date d'intégration.

| Section | Items | Propriétaire |
|---|---|---|
| **IT** | Bitlocker activé · Profil Kaspersky activé avec licence · Filtrage web activé · Agent GLPI installé et opérationnel · OneDrive actif et opérationnel · Logiciels requis installés *(selon profil)* | RH spécialisé IT du pôle |
| **Messagerie** | Double authentification en place · Office 365 activé | RH spécialisé IT du pôle |
| **Active Directory** | Restrictions ports USB *(selon profil)* · Restrictions installation d'exécutables *(selon profil)* · Restrictions changement date/heure · Restrictions accès registre · VLAN configuré selon le poste · Date/heure synchronisée NTP | RH spécialisé IT du pôle |
| **Pointage** | Pointage facial mis en place | RH du pôle |
| **Sensibilisation SI** | Sensibilisation sécurité de l'information effectuée · Politique des mots de passe (10 car. min., complexes) · Politique de télétravail | RH du pôle |
| **RH** | Compte SELFRH créé · Processus disciplinaire signé · Accord de non-divulgation signé · Fiche de décharge signée | RH du pôle |

Clôture : contrôle final signé (nom, poste, signature) → trace d'audit dans `WorkflowInstance`.

## 4. Checklist Offboarding — contenu de référence

Dérivé du document réel `SMSI.ENR.10-2`. Fiche collaborateur : Nom, Poste, Date effective de départ.

| Section | Items | Propriétaire |
|---|---|---|
| **Poste** | Formatage complet du poste · Suppression de l'utilisateur GLPI · Formatage complet du téléphone pro *(si applicable)* | RH spécialisé IT du pôle |
| **Messagerie** | Suppression du mail | RH spécialisé IT du pôle |
| **Active Directory & Accès** | Désactivation du profil AD · Suppression des accès VPN *(si applicable)* | RH spécialisé IT du pôle |
| **Pointage** | Désactivation du pointage facial & suppression du profil | RH du pôle |
| **RH** | Compte SELFRH supprimé · Fiche de décharge (section restitution) signée | RH du pôle |

**Un seul workflow générique**, quel que soit le motif de départ (fin de contrat, démission, mobilité interne, licenciement) — fidèle au document réel, qui ne distingue pas les motifs. Pas de variante par motif dans ce prototype.

## 5. Personnalisation — référentiel Poste × Pôle × Contrat

Les items marqués *"selon profil"* sont résolus par un **référentiel** : `(Poste, Pôle, Type de contrat) → liste d'items attendus / valeurs de configuration`. Ce référentiel fait partie du **template versionné** (section 6), pas d'une règle codée en dur.

Le type de contrat (CDI/CDD/stage/alternance) fait aussi varier les items — ex. un stagiaire n'a typiquement pas de compte SELFRH ni de processus disciplinaire à signer.

## 6. Circuit de validation qualité des templates

Reproduit le circuit réel (Rédacteur/Vérificateur/Approbateur), adapté aux 3 comptes Admin/Qualité disponibles :

1. **Rédacteur** = le RH qui propose une création/modification de template (nouveau référentiel poste, nouvel item, etc.)
2. **Vérificateur** = un des 2 comptes Admin/Qualité
3. **Approbateur** = l'autre compte Admin/Qualité

Chaque template est versionné (T0 → T1 → ...), avec historique des modifications (date, motif, auteur), à l'image du document réel. Un template doit être **approuvé** avant de pouvoir instancier un nouveau `WorkflowInstance`.

## 7. Cycle de vie & archivage

Une fois l'offboarding terminé (clôture signée), le dossier du collaborateur est **archivé en lecture seule** — consultable par RH/Admin à des fins d'audit, mais plus modifiable.

## 8. Cas particuliers — à valider

Trois catégories de cas identifiées, sans comportement système tranché pour l'instant. Proposition par défaut ci-dessous, à confirmer avant implémentation :

| Cas | Proposition (à valider) |
|---|---|
| **Mutation inter-pôle** (le collaborateur change de département en cours de checklist) | Le `WorkflowInstance` en cours est réassigné au RH du nouveau pôle ; les items déjà cochés restent acquis ; un événement d'audit trace le transfert. |
| **Annulation/suspension** (embauche annulée après le début du processus, départ repoussé) | Le `WorkflowInstance` passe à un statut `Cancelled`/`Suspended` explicite (pas de suppression) — conserve la trace, permet une reprise si le départ/l'arrivée est simplement repoussé. |
| **Pôle vacant** (RH en congé, poste non pourvu) | Les Admin/Qualité héritent temporairement des droits RH sur ce pôle jusqu'à réassignation. |

## 9. Portée et garde-fous de l'agent conversationnel

**L'agent est informatif uniquement** — il ne déclenche **aucune action destructrice ou irréversible** depuis la conversation (pas de suppression de compte, pas de révocation d'accès, pas de signature). Règles à respecter dans le prompt système / l'orchestration, pour éviter les dérives constatées en V7 :

- **RAG toujours sourcé** : toute réponse s'appuyant sur la base de connaissances doit être traçable à un chunk source ; si l'information n'est pas dans le corpus, l'agent le dit explicitement plutôt que d'inventer (anti-hallucination — c'est précisément ce qui avait échoué silencieusement en V7, cf. `HISTORIQUE.md`).
- **Statut de dossier = lecture seule** : l'agent peut *lire* l'état d'un `WorkflowInstance` (ex. "où en est mon onboarding ?") via un outil dédié, mais ne peut pas cocher un item à la place d'un RH/IT.
- **Notifications** : l'agent a connaissance du flux de notifications de l'utilisateur connecté (son pôle, son domaine) et peut orienter/expliquer, mais l'exécution d'une action métier depuis une notification reste un geste utilisateur explicite, pas une action autonome de l'agent.
- **RBAC respecté par l'agent** : un Employee ne peut pas, via le chat, obtenir des informations sur le dossier d'un autre collaborateur ; seuls RH (sur son pôle) et Admin/Qualité ont une vue élargie.
- **Escalade** : en cas de question ambiguë ou hors du périmètre documentaire/outillé, l'agent renvoie vers le RH du pôle plutôt que de répondre au jugé.

## 10. Priorités de développement

- **Ordre de construction** : socle métier d'abord (modèle de données, workflow engine, RBAC) sans IA, pour avoir une base fonctionnelle et testable rapidement — l'IA se greffe ensuite.
- **Si le temps manque en fin de stage** : le pipeline RAG et l'agent conversationnel (+ son orchestration) sont la priorité absolue à sécuriser — c'est la partie la plus différenciante pour le rendu du PFA. Le socle métier doit néanmoins être posé tôt et solidement car tout en dépend.
- **Tests** : même rigueur que V7 — xUnit + Moq + FluentAssertions sur toute nouvelle logique métier.

## 11. Ouvert / en attente

- Temps restant sur le stage et livrables attendus (rapport écrit, soutenance, dépôt de code, démo live) — pas encore communiqué, à préciser pour calibrer le périmètre final.
- Noms définitifs des ~5 pôles/départements.
- Comportements des cas particuliers (section 8) — propositions à valider.
- Contenu exact du corpus RAG et jeu de questions/réponses "gold" d'évaluation — à rédiger.
