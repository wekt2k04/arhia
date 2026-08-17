# Politique d'Onboarding — Sécurité de l'Information et Intégration Collaborateur

**Référence document :** SMSI.POL.01 · **Version :** V8-T0 · **Date d'application :** 15/08/2026
**Rédacteur :** Rôle RH (pôle concerné) · **Vérificateur :** Rôle Admin/Qualité · **Approbateur :** Rôle Admin/Qualité

## 1. Objet et périmètre

Cette politique décrit le processus d'intégration (onboarding) applicable à tout nouveau collaborateur de l'entreprise, quel que soit son poste, son pôle de rattachement ou son type de contrat (CDI, CDD, stage, alternance). Elle s'applique dès la validation de l'embauche et jusqu'à la clôture formelle du dossier d'intégration dans l'application AGIRH.

L'onboarding poursuit trois objectifs : sécuriser l'accès du collaborateur aux ressources de l'entreprise dans le respect du Système de Management de la Sécurité de l'Information (SMSI), garantir sa conformité administrative et RH dès son arrivée, et lui offrir une expérience d'intégration claire et sans oubli.

## 2. Principes directeurs

### 2.1 Personnalisation du parcours

Le parcours d'onboarding n'est pas uniforme : la checklist générée par AGIRH est résolue automatiquement selon trois critères combinés — le **poste**, le **pôle** de rattachement et le **type de contrat**. Certains items ne s'appliquent qu'à certains types de contrat (par exemple, un stagiaire ne signe pas de processus disciplinaire ni n'ouvre de compte SELFRH complet). Cette résolution est portée par le référentiel du template d'onboarding, versionné et validé selon le circuit qualité décrit en section 5.

### 2.2 Un dossier, un pôle, un RH référent

Chaque collaborateur est rattaché à un pôle unique. Le RH de ce pôle est responsable de la création de la fiche collaborateur dans AGIRH et du suivi de son onboarding. Un RH ne peut instancier ou suivre un onboarding que pour les collaborateurs de son propre pôle — cette règle est appliquée techniquement par le système (contrôle de portée), pas seulement organisationnellement.

## 3. Rôles et responsabilités

| Rôle | Responsabilité dans l'onboarding |
|---|---|
| **RH du pôle** | Crée la fiche collaborateur, déclenche l'instanciation de la checklist, coche les items relevant de sa responsabilité (RH, sensibilisation, pointage), sert de point de contact managérial |
| **RH spécialisé IT** (au sein du pôle) | Traite les items techniques de la checklist (poste de travail, messagerie, Active Directory) |
| **Admin/Qualité** | Valide et fait évoluer les templates de checklist (circuit qualité), peut intervenir sur n'importe quel pôle en cas de besoin |
| **Collaborateur** | Fournit les documents administratifs requis, prend connaissance des politiques internes, complète les formations obligatoires |

## 4. Étapes du parcours d'intégration

### 4.1 Avant l'arrivée

Dès que l'embauche est confirmée, le RH du pôle crée la fiche du futur collaborateur dans AGIRH (nom, prénom, matricule, poste, pôle, type de contrat, date d'intégration). Cette création instancie automatiquement la checklist adaptée à son profil, à partir du dernier template approuvé pour le type « Onboarding ».

En amont du jour J, le RH spécialisé IT prépare le poste de travail et les accès nécessaires, selon les items de la section IT de la checklist (voir document SMSI.PROC.04 — Procédures IT et Sécurité).

### 4.2 Jour J

Le collaborateur est accueilli, prend connaissance du présent document ainsi que de la Charte Collaborateur (SMSI.CHA.05). Les premiers items administratifs (accord de non-divulgation, fiche de décharge du matériel remis) sont signés dès ce jour.

### 4.3 Première semaine

Les accès techniques sont vérifiés et activés (double authentification, Office 365, VLAN, restrictions selon profil). Le collaborateur est sensibilisé à la sécurité de l'information — ce module est obligatoire et tracé dans la checklist, quel que soit le poste occupé.

### 4.4 Clôture du dossier

L'onboarding est considéré terminé lorsque tous les items de la checklist ont été cochés (statut OK ou KO avec commentaire justificatif — un item KO n'empêche pas la clôture s'il est dûment justifié, par exemple un logiciel non pertinent pour le poste). La clôture est réalisée par le RH du pôle. Une fois clôturé, le dossier reste consultable mais n'est plus modifiable tant qu'il n'a pas été explicitement rouvert par une action d'administration.

## 5. Contenu de référence de la checklist

La checklist d'onboarding standard couvre six sections (détail complet dans SMSI.PROC.04 pour la partie IT et SMSI.CHA.05 pour la partie RH) :

- **IT** — chiffrement du poste, protection antivirus, filtrage web, inventaire, sauvegarde cloud, logiciels selon profil
- **Messagerie** — double authentification, activation Office 365
- **Active Directory** — restrictions selon profil (ports USB, exécutables, date/heure, registre), VLAN, synchronisation horaire
- **Pointage** — activation du pointage facial
- **Sensibilisation SI** — formation sécurité, politique des mots de passe, politique de télétravail
- **RH** — compte SELFRH, processus disciplinaire (selon type de contrat), accord de non-divulgation, fiche de décharge

## 6. Documents à fournir par le collaborateur

Selon le type de contrat et le poste, le collaborateur peut être amené à fournir : pièce d'identité, justificatif de domicile, coordonnées bancaires (RIB), diplômes et certifications, et tout document spécifique requis par la réglementation applicable à son statut. La liste exacte est communiquée par le RH du pôle lors de la prise de contact initiale.

## 7. Documents liés

- SMSI.POL.02 — Politique d'Offboarding et Sécurité de l'Information
- SMSI.GUI.03 — Guide du RH référent de pôle
- SMSI.PROC.04 — Procédures IT et Sécurité du Poste de Travail
- SMSI.CHA.05 — Charte Collaborateur et Règlement Intérieur
