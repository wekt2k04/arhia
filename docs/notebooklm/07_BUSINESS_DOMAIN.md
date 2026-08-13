# AGIRH — Logique Métier & Workflows (V6)

> **Objectif :** maîtriser la **règle métier** d'AGIRH (RH : congés, CET, avance sur salaire, onboarding, conversations) sans lire le code.
> **Ancrage :** chaque règle cite la classe/endpoint qui l'implémente. **Source seeds :** `Program.cs:259-300` (employés `259-268`, payroll `270-282`, checklist `284-300`).

---

## 1. Résumé exécutif (3 puces)

- **AGIRH = portail RH agentique** : 3 rôles (**Admin / Manager / Collaborator**), hiérarchie via `ManagerId`, compte actif/inactif fail-closed.
- **2 domaines cœur : congés & CET** (approbation manager sur subordonnés) et **avance sur salaire** (cap 50 % du net, **un seul `Pending`** par employé).
- **L'assistant IA** exécute ces règles via le pipeline RAG/RBAC (`AgentOrchestratorService`) et produit des **marqueurs** (`||WIDGET:SalaryAdvance:{id}||`, `||SUGGEST:…||`).

---

## 2. Modèle RH (Employee)

| Règle | Valeur | Implémentation |
|---|---|---|
| Rôles | `Admin` / `Manager` / `Collaborator` (défaut `Collaborator`) | `Employee.cs` (setter garde) |
| Identifiant | `Guid`, email **unique + lowercased** | index `IX_Employees_Email` |
| Hiérarchie | `ManagerId` → `Employee.Manager` (auto-référence) | nav EF |
| Comptes | `IsActive` **défaut `true`** sur l'entité ; **fail-closed à l'extraction** : claim absent → `false` | `Employee.cs:65` / `HardStateExtractor.cs:30` |
| Solde | `LeaveBalance` (decimal) + `CompteEpargneTemps` (CET) | `Employee.cs` |
| Mots de passe | **BCrypt** `$2a$11$` (jamais en clair) | `AuthController.cs:43` |

**Comptes seed (`Program.cs:259-268`) :**
| Email | Rôle | Solde | Actif |
|---|---|---|---|
| `admin@agirh.fr` / `admin123` | Admin | — | ✅ |
| `marie.martin@agirh.fr` / `manager123` | Manager | 25 j | ✅ |
| `jean.dupont@agirh.fr` / `collab123` | Collaborator | 15 j | ✅ |
| `pierre.durand@agirh.fr` / `collab123` | Collaborator | — | ❌ (inactif) |

> Jean a un `PayrollProfile` : `NetSalary = 10000`, `MaxAdvancePercentage = 0.50`.

---

## 3. Congés & CET (LeaveRequest)

| Règle | Valeur | Implémentation |
|---|---|---|
| Types | `Conges` / `CET` / `Maladie` (défaut `Conges`) | `LeaveRequest.cs` |
| Statuts | `Pending` / `Approved` / `Rejected` / `Cancelled` (défaut `Pending`) | `LeaveRequest.cs` |
| `CanApprove` | `Status == "Pending"` (computed) | entité |
| `ValidDays` | `(EndDate - StartDate).TotalDays` (computed) | entité |
| Approbation | **Manager : subordonnés uniquement** (scope via `IEmployeeRepository` : `owner.ManagerId` == user) ; Admin : tous | `LeaveFunctions.cs:163-171`, `ZeroTrustDispatcher.cs:145-159` |

**Workflow « poser un congé »** (remédiation R5)
1. Collaborateur envoie « Je veux poser 3 jours de congés la semaine prochaine » au chat.
2. Profiler → intention `LeaveRequest` → validation params (`PreFlightValidator`) → dispatch RBAC.
3. `LeaveFunctions.PoserDemandeCongesAsync` crée la demande **`Pending`** (+ commit) ; l'approbation reste au **Manager du collaborateur**. Dates/jours manquants → clarification.
4. Réponse streamée + `done`. *(Le chip `||SUGGEST:Poser un congé||` n'est émis que pour l'intention `LeaveBalance`, pas après une pose de congé — `AgentOrchestratorService.cs:279-284`.)*

**Workflow « approuver » (Manager)** : `PUT /api/leave/{id}/approve` → vérifie que `Employee.ManagerId == currentUserId` (sinon 403) → `Approved`/`Rejected`.

---

## 4. Avance sur salaire (SalaryAdvanceRequest) — cœur métier

| Règle | Valeur | Implémentation |
|---|---|---|
| Plafond | `AmountRequested ≤ NetSalary × MaxAdvancePercentage` (**50 %** pour Jean → 5000 €) | use case `CreateSalaryAdvanceRequest` (Core pur) |
| Boundary | **5000 € accepté, 5000.01 € refusé** | test `SalaryAdvanceIntegrationTests` |
| Unicité | **Un seul `Pending` par employé** (index unique filtré + garde app) | migration `20260731182015` |
| Statuts | `Pending` / `Approved` / `Rejected` | entité + DTO |
| Date | `RequestDate` fixée à la création (single source of truth = Core) | `SalaryAdvanceRepository.AddAsync` |

**Workflow « avance sur salaire »**
1. « Je veux une avance de 2000 euros » → Profiler → intention `SalaryAdvance` → `PreFlightValidator`.
2. Dispatcher RBAC → `WorkerExecutor` → MAF `PayrollFunctions` → **use case Core** `CreateSalaryAdvanceRequest` (logique pure : cap + unicité).
3. Succès → réponse streamée + **`||WIDGET:SalaryAdvance:{id}||`** (carte frontend) + chip `||SUGGEST:Suivre ma demande||`.
4. Échec (plafond/duplicata) → message poli streamé (pas d'événement `denied` — c'est un rejet métier, pas un déni de droits).

**Consultation** : `GET /api/salary-advance/{id:guid}` → **200** propriétaire/manager/admin · **404** sinon (anti-énumération, `IsVisibleAsync`).

---

## 5. Onboarding / Checklist

| Règle | Valeur |
|---|---|
| Entité | `ChecklistItem` : `Title`, `Category` (`Administratif/IT/RH/Management`), `IsRequired`, `Order` |
| Accès | `Admin` + `Manager` (tool `GenererChecklistAsync`) |
| Usage | L'IA génère la checklist d'onboarding filtrée par catégorie (`ChecklistFunctions.cs:17` `Admin\|Manager`) |

> **Seed (R4)** : **11 items** (IT 3, Administratif 3, RH 3, Management 2) dans `Program.cs:284-300`. Réponse sans résultat (« Aucune… ») **interceptée** : message verbatim, skip synthèse/checker (`AgentOrchestratorService.cs:196-205`). Catégorie hors whitelist → refus `PreFlightValidator`.

---

## 6. Conversations (historique du chat)

| Règle | Valeur | Implémentation |
|---|---|---|
| Limites par rôle | **3 Collaborator · 5 Manager · 7 Admin** (serveur) | `ConversationsController` / repo |
| Isolation | Un user ne voit **que ses conversations** (403 IDOR) | `ConversationsController` |
| Persistance | `AgentConversation` + `AgentMessage` (`role/content/timestamp`) | migration `20260731162536` |
| Mémoire | `previousMessages ≤ 20` envoyés au chat | `frontend/src/lib/api/chat.ts` |

---

## 7. Synthèse des workflows (séquences)

| Cas | Entrée | Intention | Chemin | Sortie |
|---|---|---|---|---|
| Salutation | « Bonjour » | `Greeting` (Règle 6) | LLM → GeneralChat → synthèse → checker | Réponse polie validée (~3 appels LLM) |
| Solde congés | « Quel est mon solde de congés ? » | `LeaveBalance` | pipeline → `ConsulterSoldeAsync` | Tokens + `||SUGGEST:Poser un congé||` |
| Avance | « Avance de 2000 € » | `SalaryAdvance` | pipeline → use case Core | Succès + `||WIDGET:SalaryAdvance:{id}||` |
| Déni de droits | « Révoque les accès IT » (Collaborator) | `ITAccessRevocation` | **Dispatcher rejette** → sentinelle | **frame SSE `denied`** → bulle orange |
| Rejet métier | Avance > plafond | `SalaryAdvance` | use case rejette | Message poli normal (pas de `denied`) |
| Ambigüité | Intention non routable | `IntentUnresolvable` | orchestrateur | Phrase de clarification du dispatcher (« Pouvez-vous préciser votre demande ?… ») |

> **Nuance clé** : `denied` (bulle orange) = déni **de droits** (Dispatcher) ; un rejet **métier** (plafond, doublon) reste une réponse `token` polie.

---

## 8. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0 | `07_BUSINESS_DOMAIN` créé (modèle RH, congés, avance, onboarding, conversations, workflows) | ✅ |
| 1→5 | Docs restants (voir `00_README_INDEX`) | en cours |
