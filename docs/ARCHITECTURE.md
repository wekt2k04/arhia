# Architecture — AGIRH V8

*Document de cadrage suite à `LOGIQUE_METIER.md` et `STACK_TECHNIQUE.md`. Traduit les décisions produit/stack en structure de code concrète (couches, dossiers, ports) et en diagrammes des flux principaux. Document vivant — à ajuster dès que l'implémentation révèle un écart. Sert de référence à `hexagonal-architect` (`.claude/agents/`).*

## 1. Principe hexagonal

Même règle de flèche que V7 (`.claude/agents/hexagonal-architect.md`) : dépendance acyclique, jamais vers le haut.

```mermaid
graph TB
    Domain["Agirh.Domain<br/>(pur — zéro NuGet externe)"]
    Core["Agirh.Core<br/>(ports, RbacMatrix, use cases)"]
    Infra["Agirh.Infrastructure<br/>(EF Core, Qdrant, ONNX, Ollama, SSE)"]
    Api["Agirh.Api<br/>(composition root, Controllers)"]
    Front["frontend/ (Next.js, BFF)"]

    Core --> Domain
    Infra --> Core
    Infra --> Domain
    Api --> Infra
    Api --> Core
    Api --> Domain
    Front -.HTTP/SSE.-> Api
```

**Domain** : entités pures — `Employee`, `Department`, `WorkflowTemplate`, `WorkflowInstance`, `ChecklistItem`, `ItemStatus`, `Notification`. Aucune dépendance externe, aucune logique de persistance.

**Core** : ports (interfaces) + use cases + RBAC. Ne connaît que Domain.

**Infrastructure** : un adaptateur par port. Aucune règle métier — traduction mécanique uniquement.

**Api** : composition root (`Program.cs`, DI), Controllers qui orchestrent sans logique métier.

## 2. Arborescence cible

```
src/
  Agirh.Domain/
    Entities/          Employee, Department, WorkflowTemplate, WorkflowInstance,
                        ChecklistItem, ItemStatus, Notification, UserAccount
    ValueObjects/       EmployeeNumber, ContractType, NomPole (readonly record struct)
    Enums/              RoleType (Employee|HR|QualityAdmin), WorkflowStatus
                        (InProgress|Closed|Archived|Cancelled|Suspended), ItemStatus (Done|Failed|Pending),
                        TemplateStatus (Draft|InReview|Approved|Rejected)

  Agirh.Core/
    Ports/              IWorkflowInstanceRepository, ITemplateRepository, IEmployeeRepository,
                        IDepartmentRepository, IVectorSearchPort, IEmbeddingPort, IRerankerPort,
                        ILlmRouterPort, ILlmGeneratorPort, INotificationPort, IAuditTrailPort
    Security/           RbacMatrix (3 rôles), DepartmentScopeGuard (RH → son pôle uniquement)
    UseCases/           CreateEmployeeRecord, InstantiateWorkflow, CheckItem,
                        ProposeTemplate (Rédacteur), ValiderTemplate (Vérificateur/Approbateur),
                        ArchiveCase, ResoudreReferentielItems (Poste×Pôle×Contrat)

  Agirh.Infrastructure/
    Persistence/        AgirhDbContext (SQL Server, EF Core), implémentations des repositories
    Rag/                MarkdownChunker (structurel + recouvrement), OnnxEmbeddingAdapter,
                        QdrantVectorSearchAdapter, OnnxRerankerAdapter
    Llm/                OllamaRouterAdapter, OllamaGeneratorAdapter
    Realtime/           SseNotificationBroadcaster
    Logging/            TechnicalLogAdapter, AuditTrailAdapter (deux flux séparés, STACK_TECHNIQUE.md §6)

  Agirh.Api/
    Controllers/        AuthController, EmployeeController, WorkflowController,
                        TemplateController, ChatController (SSE), NotificationController (SSE)
    Program.cs           composition root

frontend/
  app/
    (public)/            page de garde
    chat/                 interface post-connexion : chat + barre de notifications
  lib/api/                BFF (cookie httpOnly, jamais le JWT exposé au client)

rag/corpus/                 corpus source du pipeline RAG (6 documents Markdown, milestone 7/9 CHECKLIST.md) —
                            lu par l'adaptateur d'ingestion (Agirh.Infrastructure/Rag/), jamais par le code applicatif directement

tests/
  Agirh.Tests/            xUnit + Moq + FluentAssertions, miroir de la structure Core/Infrastructure/Api
```

**Statut : structure cible, pas encore créée.** Vérifier avec `Glob` avant de supposer qu'un de ces dossiers existe.

## 3. Modèle métier — entités et relations

Le diagramme ci-dessous décrit le modèle réellement porté par `Agirh.Domain/Entities`. Les
cardinalités `1`/`0..1`/`*` indiquent respectivement une relation obligatoire, optionnelle ou
multiple. `TemplateSection` et `TemplateItem` appartiennent à un `WorkflowTemplate` ;
`ChecklistItemStatus` appartient à un `WorkflowInstance` et constitue la copie opérationnelle
d'un item de template au moment de l'instanciation.

```mermaid
classDiagram
  direction LR

  class Department {
    +Guid Id
    +string Name
    +Rename(newName)
  }

  class UserAccount {
    +Guid Id
    +string Email
    +RoleType Role
    +Guid? DepartmentId
    +bool IsActive
    +ElevateRole(newRole, newDepartmentId)
    +Deactivate()
    +Reactivate()
  }

  class Employee {
    +Guid Id
    +EmployeeNumber EmployeeNumber
    +string LastName
    +string FirstName
    +string JobTitle
    +Guid DepartmentId
    +ContractType ContractType
    +DateTime StartDate
    +DateTime? DepartureDate
    +Guid? UserAccountId
    +RecordDeparture(departureDate)
    +ChangeDepartment(newDepartmentId)
    +LinkUserAccount(userAccountId)
  }

  class WorkflowTemplate {
    +Guid Id
    +WorkflowType Type
    +string Version
    +TemplateStatus Status
    +Guid AuthorId
    +Guid? VerifierId
    +Guid? ApproverId
    +DateTime CreatedAt
    +Submit()
    +Verify(verifierId)
    +Approve(approverId)
    +Reject(actorId, reason)
    +ResolveApplicableItems(contractType)
  }

  class TemplateSection {
    +Guid Id
    +string Name
    +int Order
  }

  class TemplateItem {
    +Guid Id
    +string Label
    +int Order
    +IReadOnlyCollection~ContractType~ ApplicableContractTypes
    +IsApplicableFor(contractType) bool
  }

  class WorkflowInstance {
    +Guid Id
    +Guid EmployeeId
    +Guid TemplateId
    +string TemplateVersion
    +WorkflowType Type
    +WorkflowStatus Status
    +DateTime CreatedAt
    +DateTime? ClosureDate
    +Check(itemId, status, checkedBy, checkedDate, comment)
    +Close(closureDate)
    +Archive()
    +Cancel()
    +Suspend()
    +Resume()
  }

  class ChecklistItemStatus {
    +Guid Id
    +Guid TemplateItemId
    +string Label
    +ItemStatus Status
    +string? Comment
    +Guid? CheckedBy
    +DateTime? CheckedDate
    +Check(status, checkedBy, checkedDate, comment)
  }

  class EmployeeNumber {
    +string Value
  }

  Department "1" --> "0..*" Employee : appartient à
  Department "1" --> "0..*" UserAccount : rattache les RH
  UserAccount "0..1" --> "0..1" Employee : compte lié
  Employee "1" --> "0..*" WorkflowInstance : possède
  WorkflowTemplate "1" *-- "1..*" TemplateSection : organise
  TemplateSection "1" *-- "1..*" TemplateItem : contient
  WorkflowTemplate "1" --> "0..*" WorkflowInstance : modèle de
  WorkflowInstance "1" *-- "1..*" ChecklistItemStatus : contient
  TemplateItem "1" --> "0..*" ChecklistItemStatus : origine logique
  Employee "1" *-- "1" EmployeeNumber : identifiant métier

  note for WorkflowTemplate "Seul un template Approved peut être instancié."
  note for TemplateItem "Aucune restriction = applicable à tous les contrats."
  note for WorkflowInstance "Dossier concret : Onboarding ou Offboarding."
  note for ChecklistItemStatus "Etat initial : Pending ; puis Done ou Failed."
```

Le lien `TemplateItem → ChecklistItemStatus` est une traçabilité logique via `TemplateItemId`.
En revanche, dans le mapping EF Core actuel, les items de checklist sont possédés par
`WorkflowInstance` et cette référence n'est pas configurée comme une clé étrangère vers
`TemplateItem`.

## 4. Flux — création d'un onboarding et checklist

```mermaid
sequenceDiagram
    participant RH as RH du pôle
    participant Api as WorkflowController
    participant UC as InstantiateWorkflow (use case)
    participant Ref as ResoudreReferentielItems
    participant DB as SQL Server

    RH->>Api: Créer fiche collaborateur (Poste, Pôle, Contrat, Date)
    Api->>UC: CreateEmployeeRecord + InstantiateWorkflow
    UC->>Ref: Résoudre items selon (Poste × Pôle × Contrat)
    Ref-->>UC: Liste d'items attendus (template approuvé)
    UC->>DB: Persister WorkflowInstance + ChecklistItem[]
    DB-->>RH: Checklist instanciée (statut InProgress)
```

## 5. Flux — circuit de validation d'un template

```mermaid
sequenceDiagram
    participant RH as RH (Rédacteur)
    participant V as Admin/Qualité #1 (Vérificateur)
    participant A as Admin/Qualité #2 (Approbateur)
    participant DB as SQL Server

    RH->>DB: ProposeTemplate (nouvelle version, statut=Draft)
    DB-->>V: Notification SSE — template en attente
    V->>DB: ValiderTemplate (statut=InReview → Vérifié)
    DB-->>A: Notification SSE — prêt pour approbation
    A->>DB: ValiderTemplate (statut=Approved, version T(n) figée)
    Note over DB: Seul un template Approved peut instancier un WorkflowInstance (LOGIQUE_METIER.md §6)
```

## 6. Flux — question conversationnelle (RAG + Router/Generator)

```mermaid
sequenceDiagram
    participant U as Utilisateur (chat)
    participant Api as ChatController (SSE)
    participant Router as Router (Ollama, petit modèle)
    participant RAG as Pipeline RAG (4 phases)
    participant Gen as Generator (Ollama)
    participant Stat as WorkflowInstance (lecture seule)

    U->>Api: Question (stream SSE ouvert)
    Api->>Router: Classifier l'intention
    alt question documentaire
        Router->>RAG: Chunking déjà fait à l'ingestion → Embedding requête → Qdrant (top-K) → Reranking ONNX
        RAG-->>Gen: Chunks rerankés (sourcés)
    else statut de dossier
        Router->>Stat: Lecture WorkflowInstance (RBAC : pôle du RH ou dossier de l'Employee)
        Stat-->>Gen: État du dossier
    end
    Gen-->>Api: Réponse (streamée frame par frame)
    Api-->>U: SSE frames + frame terminale
    Note over Gen: Si aucun chunk pertinent retourné → "je n'ai pas trouvé cette information" (anti-hallucination, LOGIQUE_METIER.md §9)
```

## 7. RBAC — schéma de portée

```mermaid
graph LR
    Collab["Employee<br/>(ses propres données)"]
    RH["RH<br/>(son pôle uniquement)"]
    Admin["Admin/Qualité<br/>(portée globale)"]

    Admin -->|élève le rôle de| Collab
    Admin -->|élève le rôle de| RH
    RH -->|gère| Collab
```

Un RH qui cible un `WorkflowInstance` hors de son pôle → refus (`DepartmentScopeGuard`), avant même la vérification RBAC de rôle. Voir `.claude/agents/secops-guardian.md`.

## 8. Ouvert / en attente

- Nommage exact des migrations EF Core et des collections Qdrant — à fixer à l'implémentation.
- Découpage précis des Controllers si un module grossit (ex. séparer `TemplateController` en lecture/écriture) — non bloquant pour démarrer.
- Les 3 cas particuliers (LOGIQUE_METIER.md §8 : mutation inter-pôle, annulation/suspension, pôle vacant) n'ont pas encore de use case dédié — à ajouter dans `Agirh.Core/UseCases/` une fois leur comportement validé.
