# 

03 \- Guide Managérial Onboarding et Offboarding

Ce guide formalise le rôle des managers de proximité (Engineering Managers, Tech Leads, Directeurs de Projet) d'AGIRH dans la gestion du cycle de vie des collaborateurs, à l'intersection de l'efficacité RH et de la sécurité des systèmes d'information \[cite: 9\].

## **1\. Checklists Managériales d'Arrivée (Onboarding)**

La gestion de la sécurité commence en amont de la prise de poste, conformément à la norme ISO 27001:2022 (Annexe A.6). Le manager doit superviser un parcours minutieusement phasé :

> * **Pré-intégration (J-7 à J-1) :** Première ligne de défense incluant la vérification des antécédents (Background Screening \- ISO A.6.1) pour prévenir les menaces internes, la finalisation du contrat avec responsabilités de sécurité (ISO A.6.2), et le provisionnement des accès selon le principe du moindre privilège.  
> * **Jour J (J0) :** Sanctuarisé pour l'accueil humain et la sensibilisation formelle à la cybersécurité (ISO A.6.3), notamment sur le phishing et l'ingénierie sociale.  
> * **Semaine 1 :** Immersion technique (cartographie applicative, Pull Requests, Secure Coding ISO A.8.28) pour éviter l'introduction de vulnérabilités dans les pipelines CI/CD.  
> * **Suivi M-1 à M-3 :** Progression vers la pleine autonomie (30 jours : apprentissage ; 60 jours : rituels agiles ; 90 jours : impact roadmap).

**Welcome Pack et Conformité :** Le kit d'accueil doit inclure le Règlement Intérieur, la Charte IT et la PSSI. Une décharge signée est obligatoire pour matérialiser l'acceptation contractuelle (ISO A.6.2). Les formations doivent couvrir la protection des données selon la **loi marocaine 09-08** et le **RGPD**.

> * Vérifier l'affectation et la configuration du matériel et des logiciels avant le Jour J \[cite: 9\].  
> * Gérer les accès aux canaux (Slack, Teams) et aux environnements de test \[cite: 9\].  
> * Assigner un premier périmètre d'intervention délimité et organiser la présentation aux équipes transverses pour éviter le syndrome du "nouveau laissé à l'abandon" \[cite: 9\].

## **2\. Rituels de Suivi de la Performance**

La gestion de la performance dans une équipe tech agile nécessite des points continus \[cite: 9\] :

> * **Rapport d'étonnement :** Planifié entre la semaine 2 et 4, il sert d'audit via le regard neuf de l'employé pour détecter des lourdeurs architecturales, de la dette technique ou des failles de sécurité \[cite: 9\].  
> * **Points "1-on-1" :** Réunions bilatérales courtes (hebdomadaires ou bi-mensuelles) pour évaluer l'engagement du collaborateur, anticiper les conflits ou risques de burnout (surtout pour les consultants ESN), et préparer les décisions sur la période d'essai ou un Plan d'Amélioration de la Performance (PIP) \[cite: 9\].

## **3\. Responsabilités de Départ et Restitution (Offboarding)**

Conformément au contrôle ISO A.6.5, le manager est l'initiateur du processus de sécurité de fin de contrat \[cite: 9\]. Ses missions sont :

> * **Révocation coordonnée :** Le manager et la DSI doivent s'assurer que les accès au VPN, au code source, à la production et à la messagerie sont suspendus au moment exact du départ, afin d'éradiquer les comptes actifs dangereux (Ghost Accounts) \[cite: 9\].  
> * **Restitution du matériel (ISO A.5.11) :** Dresser un inventaire contradictoire et signer un récépissé pour les PC (chiffrés via BitLocker/FileVault), disques durs, téléphones et clés de sécurité MFA (YubiKeys) \[cite: 9\].  
> * **Transfert opérationnel :** Réassigner expressément les rôles (ex. Product Owner, Scrum Master) et les tickets associés (Jira) pour assurer la continuité de service des solutions logicielles \[cite: 9\].