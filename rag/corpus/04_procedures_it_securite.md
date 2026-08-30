# Procédures IT et Sécurité du Poste de Travail

**Référence document :** SMSI.PROC.04 · **Version :** V8-T0 · **Date d'application :** 15/08/2026
**Rédacteur :** Rôle RH spécialisé IT · **Vérificateur :** Rôle Admin/Qualité · **Approbateur :** Rôle Admin/Qualité

## 1. Objet

Ce document détaille les procédures techniques associées aux items IT des checklists d'onboarding et d'offboarding (SMSI.POL.01 et SMSI.POL.02). Il s'adresse en priorité au RH spécialisé IT de chaque pôle, responsable de leur exécution.

## 2. Poste de travail — onboarding

### 2.1 Chiffrement du disque (Bitlocker)

Tout poste de travail remis à un collaborateur doit avoir le chiffrement de disque activé avant remise. Cette vérification est un item bloquant de la section IT de la checklist d'onboarding.

### 2.2 Protection antivirus

Le profil antivirus doit être activé avec une licence valide avant la mise à disposition du poste. Aucune exception n'est prévue, quel que soit le type de contrat.

### 2.3 Filtrage web

Le filtrage des contenus web est activé par défaut sur tous les postes, conformément à la politique de sécurité de l'information. Des exceptions ponctuelles peuvent être demandées via la procédure d'exception IT (hors périmètre du présent document).

### 2.4 Inventaire du parc

Chaque poste doit être déclaré et opérationnel dans l'outil d'inventaire du parc informatique avant remise au collaborateur, afin de garantir la traçabilité du matériel tout au long de son cycle de vie.

### 2.5 Sauvegarde cloud

La solution de sauvegarde cloud d'entreprise est activée et opérationnelle dès la remise du poste, afin de sécuriser les données professionnelles du collaborateur dès son premier jour.

### 2.6 Logiciels selon profil de poste

La liste des logiciels à installer dépend du poste occupé et du pôle de rattachement — elle est résolue automatiquement par le référentiel de checklist d'arhia selon la combinaison poste/pôle/type de contrat. Le RH spécialisé IT vérifie que l'ensemble des logiciels requis pour le profil concerné sont bien installés avant de cocher cet item.

## 3. Messagerie — onboarding

- **Double authentification** : activée obligatoirement sur le compte de messagerie avant le premier jour du collaborateur.
- **Suite bureautique en ligne** : le compte est activé et accessible dès l'arrivée.

## 4. Active Directory — onboarding

Plusieurs restrictions sont appliquées **selon le profil du poste**, et ne sont donc pas identiques pour tous les collaborateurs :

- Restriction des ports USB
- Restriction de l'installation d'exécutables
- Restriction du changement de date et heure du poste
- Restriction de l'accès au registre système

D'autres réglages s'appliquent uniformément à tous les postes :

- **VLAN** : configuré selon le poste occupé, pour un cloisonnement réseau cohérent avec le rôle du collaborateur.
- **Synchronisation horaire** : le poste doit être synchronisé avec le serveur de temps de référence de l'entreprise.

## 5. Pointage — onboarding

Le pointage facial est activé pour chaque nouveau collaborateur, indépendamment de son poste ou de son type de contrat, dans le respect de la politique de gestion du temps de travail.

## 6. Poste de travail — offboarding

À la date effective de départ du collaborateur :

- **Formatage complet du poste** de travail avant réaffectation ou stockage.
- **Suppression de l'utilisateur** dans l'outil d'inventaire du parc.
- **Formatage du téléphone professionnel**, le cas échéant, si un tel équipement avait été confié au collaborateur.

## 7. Messagerie et accès — offboarding

- **Suppression du compte de messagerie.**
- **Désactivation du profil Active Directory.**
- **Suppression des accès VPN**, le cas échéant, si le collaborateur en disposait.

## 8. Pointage — offboarding

Le profil de pointage facial est désactivé et le profil biométrique associé supprimé, sans délai après le dernier jour de présence du collaborateur.

## 9. Politique des mots de passe

Tout mot de passe d'accès aux systèmes de l'entreprise doit respecter les règles suivantes : dix caractères minimum, association d'au moins trois catégories parmi majuscules, minuscules, chiffres et caractères spéciaux. Cette politique est présentée au collaborateur lors du module de sensibilisation à la sécurité de l'information (voir SMSI.POL.01, section 4.3).

## 10. Politique de télétravail

L'éligibilité au télétravail dépend du poste occupé et des accès techniques associés (notamment l'accès VPN). Les modalités précises (fréquence, équipement fourni, engagements de sécurité) sont communiquées au collaborateur par son RH de pôle lors du module de sensibilisation.

## 11. Documents liés

- SMSI.POL.01 — Politique d'Onboarding et Sécurité de l'Information
- SMSI.POL.02 — Politique d'Offboarding et Sécurité de l'Information
- SMSI.GUI.03 — Guide du RH référent de pôle
