# AGIRH — Sécurité & RBAC (V6)

> **Objectif :** maîtriser l'auth (JWT/BCrypt), le contrôle d'accès (RBAC/scope), et les **notions de robustesse sécurité** (IDOR, TOCTOU, anti-énumération, fail-closed) — ancré au code.
> **Frontend :** le JWT ne vit que dans un **cookie httpOnly** (BFF Next.js) — jamais dans le JS.

---

## 1. Résumé exécutif (3 puces)

- **Auth** : JWT HS256 (claims standard + `is_active`/`managerId`, 8 h) + **BCrypt** ; **FallbackPolicy secure-by-default** (toute route exige un JWT sauf `[AllowAnonymous]`).
- **RBAC** : matrice unique `RbacMatrix.Default` → `ZeroTrustDispatcher` fail-closed ; scope `self/subordonnés/Admin` ; déni RBAC = événement SSE **`denied`**.
- **Défenses** : rate-limit (429), anti-énumération (**404 seul**), **TOCTOU** (index filtré), `ex.Message` masqué.

---

## 2. Authentification

### JWT (`JwtTokenService.cs`)
| Élément | Valeur |
|---|---|
| Algorithme | HS256 (clé ≥ 32 caractères, `Jwt:Key`) |
| Expiration | `Jwt:ExpiryHours` (défaut **8 h**, clampé [1,24]) |
| Claims | `nameid` (Id), `email`, `given_name`, `family_name`, `role`, `is_active`, `managerId`?, `exp` |
| Validation | issuer + audience + lifetime + signature, **ClockSkew = 1 min** (`Program.cs:76-86`) |

### Login (`AuthController.cs`)
| # | Check | Réponse |
|---|---|---|
| 1 | corps/email vide | `400 {Message:"Email requis"}` |
| 2 | format email | `400 {Message:"Format d'email invalide"}` |
| 3 | employé absent / inactif | `401 {Message:"Email invalide ou compte inactif"}` |
| 4 | mauvais mot de passe | `401 {Message:"Email ou mot de passe invalide"}` |
| 5 | OK | `200 { token, employeeId, role, firstName, lastName }` (**camelCase**) |

> **Anti-énumération login** : les messages 3 et 4 sont **volontairement différents** côté serveur mais le BFF frontend les **normalise en un seul** message générique ; **429** = message distinct « Trop de tentatives… ».

### BCrypt
- Stockage : `Employee.PasswordHash`, hash `$2a$11$` (facteur 11, sel 128 bits).
- Vérification **time-constant** (`BCrypt.Net.BCrypt.Verify`) → pas de timing attack.
- Package : `BCrypt.Net-Next`.

### Cycle de vie frontend (cookie httpOnly)
```
Login → BFF /api/auth/login → backend → setAuthCookie(agirh_token, httpOnly, SameSite=Lax, **Secure en prod uniquement** — `cookies.ts:81,91`)
Restore → /api/auth/session (décode + valide exp)
Logout  → purge cookie → 204
Appels  → BFF /api/agent/* et /api/[...path] injectent Authorization: Bearer côté serveur
```

| Propriété | État |
|---|---|
| Token accessible au JS | ❌ (cookie HttpOnly, `document.cookie` vide) |
| localStorage / sessionStorage | jamais utilisés |
| 401 pendant l'UI | `apiFetch` → redirect `/login?returnUrl=…` |

---

## 3. RBAC — source unique & dispatch

| Notion | Comportement | Ancrage |
|---|---|---|
| **Matrice unique** | `RbacMatrix.Default.ResolveTool` + `GetToolConfig`/`ParseAllowedRoles` | `ZeroTrustDispatcher.cs:78-129` |
| **Scope** | `EvaluateOwnerScope` : self OK, `ManagerId=null` → refus, **Admin exempt** | `ZeroTrustDispatcher.cs:165-175` |
| **Manager** | approbation congés **subordonnés uniquement** — scope via `IEmployeeRepository` (lecture `owner.ManagerId`) | `LeaveFunctions.cs:163-171`, `ZeroTrustDispatcher.cs:145-159` |
| **IDOR Collaborator** | self uniquement (historique congés, solde) | gardes MAF + contrôleurs |
| **Fail-closed** | `DispatchRejected` → sentinelle `\u001fDENIED\u001f` → **SSE `denied`** | sentinelle `AgentOrchestratorService.cs:43`, traitement `AgentController.cs:127-135` |

### Matrice des capacités (résumé)
| Capacité | Admin | Manager | Collaborator |
|---|---|---|---|
| `GET /api/employees` | ✅ | ✅ | ❌ |
| `POST /api/employees` | ✅ | ❌ | ❌ |
| `PUT /api/leave/{id}/approve` | ✅ | ✅ (subordonnés) | ❌ |
| `POST /api/admin/ingest` | ✅ | ❌ | ❌ |
| Avance → GET `{id:guid}` | ✅ | ✅ | ✅ (sa propre) |
| Chat agent | ✅ | ✅ | ✅ |
| **Bavardage (`GeneralChat`)** | ✅ | ✅ | ✅ | ⚠️ intention non mappée (`GeneralInquiry`/`SmallTalk`) → routage direct sans entrée matrice (`ZeroTrustDispatcher.cs:65-76`) — aucune donnée sensible exposée |
| **Poser un congé** (`PoserDemandeCongesAsync` — R5) | ✅ | ✅ | ✅ |

> **Ordre des gates du Dispatcher** (`ZeroTrustDispatcher.cs:28-120`) : **confiance < 0.4** → `IntentUnresolvable` → **compte inactif** → `ACCOUNT_INACTIVE` → **résolution outil** (non mappée → `GeneralChat`) → **rôle** (`(RoleFlag & RequiredRoles) == 0` → `INSUFFICIENT_ROLE`) → **scope** (Manager seulement) → **dispatch**. L'IDOR Collaborator n'est PAS géré par le dispatcher (scope réservé Manager) mais par les **gardes internes des outils MAF** (self-only : `ConsulterSoldeTool` :31-32, `ConsulterHistoriqueCongesTool` :110-111, `PoserDemandeCongesTool` :46-47) et par le use case avance (`CreateSalaryAdvanceRequest.cs:45-47`).

> **Nuance `denied`** : la **décision** de déni vient du Dispatcher, mais le **texte** de la bulle orange est **généré par l'orchestrateur** (template intention + rôle, `AgentOrchestratorService.cs:158`) — les messages spécialisés `FormatDenialMessage` (`ZeroTrustDispatcher.cs:224-235`) ne sont **jamais affichés** (loggés uniquement). Un refus de **scope MAF** (ex. consulter l'historique d'un autre) = réponse `token` polie (pas de bulle).

---

## 4. Notions de robustesse sécurité

| Notion | Définition | Application AGIRH |
|---|---|---|
| **IDOR** | Accès à la ressource d'autrui par id | chat : scope dispatcher (Manager) + gardes MAF (Collaborator) ; REST : `SalaryAdvanceController` scopé, mais `LeaveController.GetByEmployee` / `EmployeesController.GetById` laissent le **Manager lire tout employé** (divergence REST/chat assumée) ; tests dédiés |
| **Anti-énumération** | Ne pas révéler l'existence d'une ressource | `SalaryAdvanceController` : **404 seul** (corps vide) ; message « Demande introuvable ou inaccessible » normalisé côté frontend |
| **TOCTOU** | Course « check-then-act » sur l'unicité | index unique filtré `(EmployeeId) WHERE Status='Pending'` + garde app (migration `20260731182015`) |
| **Fail-closed** | Refuser par défaut | extraction claim absent → `false` (`HardStateExtractor`), Checker fail-closed, FallbackPolicy |
| **Rate-limiting** | Freiner les abus | bucket+IP : **login 5/min · chat 20/min · open 1000/min** → **429** (`Program.cs:94-125`) |
| **ClockSkew** | Tolérance d'horloge JWT | 1 min |
| **Secret management** | Aucun secret dans le dépôt | `appsettings.json` gitignoré + vidé ; clés via env |
| **Masquage des erreurs** | Jamais `ex.Message` à l'UI | `WorkerExecutor.cs:64-69`, BFF frontend |

---

## 4bis. Sécurité du pipeline IA (volet exhaustif)

### Fail-closed par étape
| Étape | Mécanisme | Preuve |
|---|---|---|
| Extraction identité | `HardState` depuis le **JWT seul** ; `is_active` absent → `false` ; échec → 401 | `HardStateExtractor.cs:10-31` ; `AgentController.cs:57-66` |
| Salutations | détection LLM (Règle 6 → `Greeting`) → **GeneralChat** → synthétiseur (règle Greeting) → checker (exemption Greeting) ; aucun texte statique, checker fail-closed | `ProfilerService.cs:306` ; `SynthesizerAgent.cs:46-47` ; `CheckerAgent.cs:44-48` |
| Routeur (Profiler) | sortie LLM mappée sur **enum fermé** (`ParseIntention` → `Unknown`) ; **sanitisation PII** avant envoi LLM (`password/banque/IBAN/NIR… → ***`) ; 2 tentatives puis fallback `Unknown` | `ProfilerService.cs:233-248,286,82-172` |
| Validation params | `PreFlightValidator` (2 passes max) ; `ValidatedJson` **prime** sur les entités extraites (P0-1) | `PreFlightValidator.cs` ; `ZeroTrustDispatcher.cs:188-203` |
| Dispatch RBAC | gates : confiance → IsActive → rôle matrice → scope Manager ; déni → `DispatchRejected` | `ZeroTrustDispatcher.cs:28-120` |
| Exécution outils | `requestingUserId` = JWT (jamais l'IA) ; exceptions → message générique ; requêtes SQL **paramétrées** (aucune injection) | `WorkerExecutor.cs:56,64-69` ; `KnowledgeDocumentRepository.cs:36-46` |
| Interception | préfixes `Erreur`/`Accès refusé`/`Action non autorisée`/`Aucun`/`Aucune`/`L'opération a été annulée` → verbatim, 0 LLM | `AgentOrchestratorService.cs:171-179` |
| Bornage contexte | `RawWorkerData` limité à **500 mots** avant Synthesizer/Checker (swap/restore) — réduit la surface d'injection et l'écho | `AgentOrchestratorService.cs:216-217,233-236` |
| Critique (Checker) | **5 chemins → `EvaluationResult(false)`** : HTTP non-2xx · contenu vide/nul · JSON introuvable · désérialisation null · exception | `CheckerAgent.cs:77-118` |
| Livraison | draft invalide **jamais streamé** (`NotStreamed`) ; sentinelle `\u001fDENIED\u001f` filtrée à 2 niveaux (contrôleur :140 + frontend `stripControlSentinel`) | `AgentOrchestratorService.cs:239-249` ; `AgentController.cs:127-140` ; `markers.ts:68-83` |

### Prompt-injection & frontière LLM
- **Seul le Profiler reçoit les messages utilisateur**, après sanitisation regex PII (`ProfilerService.cs:286`). Synthesizer/Checker ne reçoivent que `intention` + `rawData` borné + draft — **jamais les messages bruts**.
- **Sorties LLM contraintes par le code C#** : enum d'intention fermé (`ParseIntention`), mapping outil `RbacMatrix` (jamais choisi par le LLM), verdict JSON strict du Checker. → les tentatives d'injection aboutissent à `Unknown` (**3/3 bloquées** en audit réel `AGIRH_MODEL_AUDIT.md` §2, mais **non verrouillé par un test automatisé**).
- **Aucune description d'outil envoyée au modèle** → pas de surface de tool-call injection.
- **Historique conversationnel fourni par le client** (`RecentMessages`, `TakeLast(3)`, `AgentController.cs:88-105`) : entrée non vérifiée, risque contenu par l'enum fermé + RBAC + Checker (à connaître).

### Hygiène PII & logs (R8)
- **Audit JSONL** : `Message`/`Response` tronqués à **400 caractères** (`AgentController.cs:192-193`) ; champs `Models`, `ReflectionLoops`, `CheckerValid`, `WidgetId`, `Suggestion`, `Outcome`.
- **Registre log/audit** : `FileLoggerProvider` + `ChatAuditLogger` (singleton) en tête de `Program.cs` (`Program.cs:24-51`) — `agirh-api.log` / `agirh-audit.jsonl` **réinitialisés à chaque démarrage** ; `NoopChatAuditLogger` en secours si échec d'init.
- **Sanitisation en amont** : regex sensible avant tout envoi au Profiler (couvre un jeu de mots-clés **fini** — PII hors mots-clés possible).
- **Log Checker** : corps du modèle tronqué 300 caractères (`CheckerAgent.cs:88`) — peut contenir du texte RAG (PII indirecte).
- **Aucun secret** dans les prompts ni les descriptions d'outils ; clés JWT/DB hors dépôt (env).

### Sérialisation & séparation des modèles
- `format:"json"` : **actif pour le Profiler** (`ProfilerService.cs:68`), **retiré pour le Checker** (`CheckerAgent.cs:57-59`) — asymétrie volontaire (qwen3.5:9b renvoyait du contenu vide en JSON forcé).
- Budgets : Profiler 512 / Synthesizer 160 / Checker 256 (`num_predict`) + temperature 0/0.3/0 + **`think:false` racine** (jamais dans `options`).
- `MaxReflectionLoops` : 1 en config (`appsettings.json:31`), **défaut code 2** — vérifier les **2 profils** de `launchSettings.json` : `Agirh_Bureau` (Development, Ollama entreprise) / `Agirh_Maison` (local, Ollama localhost via `appsettings.local.json`).

---

## 5. Endpoints & attributs (extraits)

| Endpoint | Attr. | Comportement |
|---|---|---|
| `/api/auth/login` · `/api/health` · `/api/agent/health` | `[AllowAnonymous]` | publics **explicites** |
| `/api/agent/chat` | `[Authorize]` | tout authentifié ; SSE + `denied` |
| `/api/conversations` | `[Authorize]` | limites 3/5/7 par rôle |
| `/api/salary-advance/{id:guid}` | `[Authorize]` | 200 propriétaire · **404** sinon |
| `/api/admin/ingest` | `[Authorize(Roles="Admin")]` | Admin only |

> **FallbackPolicy** (`Program.cs:87-92`) : tout ce qui n'est pas décoré exige un JWT — une route oubliée est **refusée**, jamais ouverte.

---

## 6. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0 | `00` + `07` | ✅ |
| 1 | `01_CORE_ARCHITECTURE` | ✅ |
| 2 | `02_SECURITY_AND_RBAC` | ✅ |
| 3 | `03_AI_AND_RAG_PIPELINE` | ✅ |
| 4 | `04_QA_AND_TESTING` + `05` | ✅ |
| 5 | `06_Docker_Orchestration` | ✅ |
