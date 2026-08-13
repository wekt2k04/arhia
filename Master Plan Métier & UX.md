{J'avais l'intention d'implémenter ça, je pense que je l'ai déduit par le passé en comparant le sujet de ce qui a été déjà fait. Ne te fis pas à ce document uniquement, prends le avec des pincettes, et surtout, sers toi des autres audite pour compléter ou soustraire les informations.}

# **🗺️ Master Plan : Implémentation Métier & UX (AGIRH V6)**

> **⚠️ RÈGLE D'OR : Aucune refonte profonde ne doit être entreprise sur la "tuyauterie" (infrastructure technique).** Le socle existant (pipeline Actor-Critic, Zero-Trust, RAG, sécurité BFF, tests automatisés) est considéré comme validé, stable et verrouillé. Tout nouveau développement métier ou UI décrit ci-dessous doit s'appuyer sur cette infrastructure (via le principe Ouvert/Fermé) sans jamais, ou faiblement en altérer le cœur.

Ce document constitue la feuille de route exhaustive pour la phase 2 du projet AGIRH V6. Il détaille la logique métier stricte de l'Onboarding et de l'Offboarding, ainsi que les modules complémentaires nécessaires pour livrer une expérience produit complète (UX/UI et Administration).

## **🚀 PARTIE 1 : CŒUR MÉTIER \- ONBOARDING (Intégration)**

L'objectif est que l'agent IA puisse piloter l'arrivée d'un collaborateur, de la signature de son contrat jusqu'à sa pleine autonomie.

### **1.1. Moteur de Checklists Dynamiques (GenererChecklistAsync)**

Le sujet exige des checklists adaptables (poste, entité, pays).

* **Entités de données à créer :** OnboardingTemplate, OnboardingTask, EmployeeOnboardingProgress.  
* **Implémentation de l'Outil MAF :** L'agent IA doit pouvoir appeler l'outil pour générer une liste de tâches personnalisée. Ex: Si le collaborateur est "Développeur au Maroc", la checklist inclura "Création compte GitHub" et "Attestation mutuelle locale".  
* **Action Agentique :** Le collaborateur demande "Qu'est-ce qu'il me reste à faire aujourd'hui ?". L'IA lit EmployeeOnboardingProgress et répond avec les tâches non cochées.

### **1.2. Suivi de la Collecte Documentaire**

* **Logique Métier :** Suivre les documents administratifs manquants (RIB, CIN, Diplômes).  
* **Outil MAF (VerifierDossierAdministratifAsync) :** L'agent peut lister les documents manquants et envoyer des rappels automatiques via le chat.

### **1.3. Provisionning IT & Logistique**

* **Logique Métier :** Demande de matériel (PC, téléphone) et d'accès logiciels.  
* **Outil MAF (DemanderMaterielITAsync) :** L'agent IA recueille le besoin du nouvel arrivant et crée un ticket dans la base de données (ou alerte le service IT).

### **1.4. Accompagnement Culturel et FAQ (Le rôle du RAG)**

* **Exploitation du RAG existant :** Le nouvel arrivant pose des questions comme "Comment fonctionne la mutuelle ?" ou "Quelle est la politique de télétravail ?".  
* **Amélioration :** Création de *Suggestion Chips* (||SUGGEST:Voir le livret d'accueil||) injectés par l'orchestrateur lors des premiers jours du collaborateur.

## **🛑 PARTIE 2 : CŒUR MÉTIER \- OFFBOARDING (Départ)**

L'offboarding est un processus critique axé sur la sécurité (désactivation) et la transmission du savoir.

### **2.1. Initialisation et Alertes Multi-Acteurs**

* **Outil MAF (InitierOffboardingAsync) :** Accessible uniquement aux Managers/Admins. Le manager annonce le départ d'un collaborateur à l'IA.  
* **Moteur de Notification :** L'outil déclenche des alertes ciblées (emails ou notifications in-app) à la paie (pour le Solde de Tout Compte), à l'IT (pour les accès) et aux services généraux (pour les badges).

### **2.2. Révocation des Accès et Sécurité**

* **Outil MAF (RevoquerAccesITAsync) :** Étendre l'outil existant pour désactiver le compte (IsActive \= false) à une date planifiée (et non de manière immédiate uniquement).  
* **Protection TOCTOU :** S'assurer via SQL Server qu'un offboarding ne peut pas être initié deux fois pour le même employé.

### **2.3. Restitution du Matériel et Passation**

* **Entités de données :** EquipmentLoan (Matériel prêté).  
* **Outil MAF (GenererQuitusDepartAsync) :** L'IA génère la liste du matériel à rendre (PC, téléphone de fonction) et permet de cocher les éléments restitués.

### **2.4. Entretien de Sortie (Exit Interview)**

* **Outil MAF (PlanifierEntretienSortieAsync) :** L'IA propose des créneaux au collaborateur sortant pour son entretien avec les RH et enregistre le rendez-vous.

## **💻 PARTIE 3 : EXPÉRIENCE COMPLÈTE (UX & Frontend)**

Pour que la "tuyauterie" devienne un véritable produit, l'interface utilisateur (Next.js) doit être finalisée.

### **3.1. Gestion de l'Identité (Authentification Complète)**

* **Page de Connexion (/login) :** Finaliser le design, gérer les états de chargement, et afficher proprement les erreurs (ex: "Compte désactivé" ou "Trop de tentatives").  
* **Module de Création de Compte / Inscription :** Une interface sécurisée (réservée aux RH) pour créer le profil d'un nouvel arrivant (qui générera son hash BCrypt et l'insèrera en base).

### **3.2. Le Module RAG Dynamique (Backoffice Admin)**

* **Problème actuel :** L'ingestion des documents se fait via des scripts ou un endpoint brut.  
* **Livrable UI :** Une page d'administration (/admin/knowledge-base) où les RH peuvent faire un *Drag & Drop* de fichiers PDF/Markdown.  
* **Livrable Backend :** Le fichier est uploadé, découpé en morceaux (*chunking* 512 mots), vectorisé en arrière-plan via embeddinggemma, et inséré dans SQL Server 2025\.

### **3.3. Tableau de Bord (Dashboard Manager)**

* L'IA conversationnelle c'est bien, mais un manager a besoin de visibilité globale.  
* **Livrable UI :** Une vue Kanban ou un tableau listant les *Onboardings en cours* (ex: 80% complété) et les *Offboardings en cours* (ex: En attente restitution PC). Les données sont tirées directement des entités modifiées par l'IA.

## **⚙️ PARTIE 4 : CHANTIERS TECHNIQUES (Sous le capot)**

*(Rappel : Ces chantiers sont des optimisations de la tuyauterie existante, pas une refonte).*

### **4.1. Unification du Routage IA**

* **Suppression de GreetingClassifier :** Supprimer l'agent C\# déterministe. Les salutations passeront désormais par le Profiler (LLM) → Intent GeneralChat → Synthesizer. Cela simplifie l'architecture de l'orchestrateur.

### **4.2. Fiabilisation Réseau des LLM**

* **Tuning de Polly :** Ajuster le *Backoff exponentiel* pour les appels à l'API d'Ollama. Actuellement, des erreurs de SocketException (Timeout) surviennent en charge.  
* **Ajustement des Prompts :** Verrouiller les instructions du modèle phi4-mini (routeur) pour s'assurer qu'il ne génère *que* du JSON valide, même lorsqu'on l'interroge sur des concepts métiers (Onboarding/Offboarding).

### **4.3. Tests Frontend (Assurance Qualité UI)**

* Mise en place de **Vitest** et **React Testing Library**.  
* Écriture de tests pour valider le parseur de marqueurs (||WIDGET||), la protection XSS (désactivation stricte des scripts dans les réponses Markdown de l'IA) et le routage protégé du middleware Next.js.