# Fondations : architecture hexagonale, RBAC, sécurité — le squelette d'AGIRH

*Document autonome — lisible sans avoir lu les autres. Il pose les fondations logicielles sur
lesquelles reposent tous les autres documents (IA, Docker, workflows) : comment le code est
organisé, qui a le droit de faire quoi, et comment l'identité d'un utilisateur circule dans le
système sans jamais être exposée à un risque évitable.*

## Pourquoi organiser du code en couches, au juste ?

Le problème que toute application de taille moyenne rencontre tôt ou tard : le code métier (les
règles réelles du métier — "un RH ne peut gérer que son pôle", "un template doit être approuvé
avant d'instancier un dossier") finit mélangé avec le code technique (comment on parle à une base
de données, comment on sérialise du JSON, comment on appelle une API externe). Ce mélange a un
coût qui grossit avec le temps : chaque changement technique (changer de base de données, changer
de fournisseur d'IA) risque de casser une règle métier qu'on ne voulait pas toucher, et inversement
tester une règle métier oblige à démarrer une vraie base de données.

L'**architecture hexagonale** (aussi appelée "ports & adaptateurs", formalisée par Alistair
Cockburn au début des années 2000) répond à ce problème par une règle simple : le code métier ne
doit **jamais** dépendre du code technique. C'est l'inverse qui est vrai : le code technique
dépend du métier, jamais le contraire. On l'appelle "hexagonale" parce que le cœur métier est
dessiné au centre d'un hexagone, entouré d'adaptateurs interchangeables sur chaque face — le
nombre six n'a rien de spécial, c'est juste une forme qui donne de la place à plusieurs façades.

Cette idée est une application directe du **principe d'inversion de dépendance** (le "D" de
SOLID) : les modules de haut niveau (les règles métier) ne doivent pas dépendre des modules de bas
niveau (base de données, réseau, fournisseurs externes) ; les deux doivent dépendre d'abstractions.

### Comment ça se traduit concrètement dans AGIRH

Le code est rangé en quatre projets .NET, avec une règle de dépendance à sens unique :

```
Agirh.Domain          → entités pures (Employee, Department, WorkflowTemplate, WorkflowInstance,
                         ChecklistItem, Notification, UserAccount). Zéro paquet NuGet
                         externe. Ce projet ne sait même pas qu'une base de données existe.

Agirh.Core            → les "ports" (des interfaces C# : IWorkflowInstanceRepository,
                         ITemplateRepository, ILlmRouterPort, ILlmGeneratorPort,
                         IVectorSearchPort...), les use cases (la logique métier elle-même :
                         CreateEmployeeRecord, InstantiateWorkflow, ValiderTemplate...), et le
                         RBAC (RbacMatrix, DepartmentScopeGuard). Ce projet ne connaît que Domain.

Agirh.Infrastructure   → un adaptateur concret par port : EF Core implémente
                         IWorkflowInstanceRepository, Qdrant implémente IVectorSearchPort, Ollama
                         implémente ILlmRouterPort et ILlmGeneratorPort. Aucune règle métier ici —
                         seulement de la traduction mécanique entre le port (l'interface abstraite)
                         et la technologie réelle.

Agirh.Api              → les Controllers ASP.NET Core (AuthController, WorkflowController,
                         TemplateController, ChatController, NotificationController) et
                         Program.cs, qui est la "composition root" : le seul endroit du code qui
                         décide quel adaptateur concret brancher derrière chaque port.
```

Un exemple concret pour rendre ça tangible : le use case `InstantiateWorkflow` (dans `Agirh.Core`)
a besoin de sauvegarder un dossier. Il ne sait pas que ce sera fait avec SQL Server — il dépend
juste de l'interface `IWorkflowInstanceRepository`. C'est `Agirh.Infrastructure` qui fournit
`EfWorkflowInstanceRepository`, une classe qui implémente cette interface avec du vrai EF Core.
`Program.cs` fait le lien : `builder.Services.AddScoped<IWorkflowInstanceRepository,
EfWorkflowInstanceRepository>()`.

**Ce que ça apporte concrètement** : le use case peut être testé avec un faux repository en
mémoire (pas besoin de base de données pour un test unitaire — c'est exactement ce que fait la
suite de tests du projet, 158 tests verts, avec des doubles Moq/FluentAssertions à la place des
vrais adaptateurs). Et si demain SQL Server est remplacé par PostgreSQL, ou Ollama par une vraie
API cloud, seul `Agirh.Infrastructure` change — `Agirh.Core` et `Agirh.Domain` restent identiques,
parce qu'ils ne connaissent que des interfaces, jamais des implémentations.

### Les forces de cette approche

- **Testabilité** — la logique métier se teste sans infrastructure réelle.
- **Remplaçabilité** — changer de technologie ne touche qu'une seule couche.
- **Lisibilité de l'intention** — en ouvrant `Agirh.Core/UseCases/`, on lit directement les règles
  métier du projet, sans bruit technique autour.
- **Frontière claire pour la revue de code** — une règle métier qui apparaîtrait dans
  `Agirh.Infrastructure` est immédiatement suspecte : ça n'a rien à y faire.

### Les faiblesses et coûts réels

- **Plus de code au départ** — une interface + une implémentation + parfois un objet de requête/
  réponse, là où un code plus direct ferait la même chose en une seule classe. Sur un petit
  prototype, ce surcoût peut sembler disproportionné ; il se rentabilise à mesure que le projet
  grossit ou que les règles métier se complexifient.
- **Ce n'est pas gratuit à apprendre** — il faut internaliser la règle de dépendance et résister à
  la tentation d'aller vite en cassant la couche (ex. appeler EF Core directement depuis un
  Controller "juste cette fois").
- **Ne résout pas tout** — l'architecture hexagonale organise *où* vit le code, elle ne dit rien
  sur *comment* modéliser correctement le métier lui-même (ça, c'est le travail du Domain-Driven
  Design, une discipline complémentaire mais distincte).

## RBAC : contrôler qui a le droit de faire quoi

**RBAC** (Role-Based Access Control) est un modèle de contrôle d'accès où les permissions ne sont
pas attribuées directement à chaque utilisateur, mais à des **rôles**, eux-mêmes attribués aux
utilisateurs. C'est le modèle le plus répandu en entreprise parce qu'il évite d'avoir à gérer des
permissions individuelles pour chaque personne — on raisonne "qu'est-ce qu'un RH peut faire ?"
plutôt que "qu'est-ce que Camille peut faire ?".

AGIRH définit 3 rôles, avec une portée strictement croissante :

- **Employee** — accès à ses propres données uniquement (son dossier, sa checklist).
- **RH** — gère les collaborateurs de **son pôle uniquement**. Un pôle correspond à un
  département/une équipe métier.
- **QualityAdmin** — portée globale sur toute l'organisation ; seul rôle habilité à élever le rôle
  d'un compte, et seul rôle impliqué dans le circuit de validation des templates (voir document
  5 pour le détail du circuit).

Un détail de conception qui mérite d'être compris en profondeur : le RBAC "pur" (juste vérifier
le rôle) ne suffit pas ici, parce qu'un RH n'a pas accès à *tout* ce qu'un RH peut normalement
faire — seulement à son pôle. C'est un cas où RBAC pur atteint sa limite et doit être complété par
une vérification de **portée** (parfois appelée ABAC — Attribute-Based Access Control — quand la
permission dépend d'un attribut de la ressource, ici son pôle, comparé à un attribut de
l'utilisateur). AGIRH implémente ça avec `DepartmentScopeGuard`, une vérification **séparée** du rôle,
appliquée **avant** même la vérification RBAC : un RH qui cible un dossier hors de son pôle est
refusé, indépendamment du fait qu'il ait techniquement le bon rôle. Cette séparation en deux
vérifications distinctes (rôle, puis portée) rend chacune plus simple à raisonner et à tester
isolément, plutôt qu'une seule vérification monolithique qui mélangerait les deux dimensions.

**Code réel — la matrice RBAC complète** (`src/Agirh.Core/Security/RbacMatrix.cs`), une seule source de
vérité pour toutes les autorisations du système, jamais de vérification de rôle dispersée ailleurs
dans le code :

```csharp
public static class RbacMatrix
{
    private static readonly IReadOnlyDictionary<ResourceAction, IReadOnlySet<RoleType>> Default =
        new Dictionary<ResourceAction, IReadOnlySet<RoleType>>
        {
            [ResourceAction.EmployeeCreate] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstantiate] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceRead] = Roles(RoleType.Employee, RoleType.HR, RoleType.QualityAdmin),
            [ResourceAction.WorkflowInstanceCheck] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceClose] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceArchive] = Roles(RoleType.HR, RoleType.QualityAdmin),
            [ResourceAction.TemplatePropose] = Roles(RoleType.HR),
            [ResourceAction.TemplateVerify] = Roles(RoleType.QualityAdmin),
            [ResourceAction.TemplateApprove] = Roles(RoleType.QualityAdmin),
            [ResourceAction.TemplateReject] = Roles(RoleType.QualityAdmin),
            [ResourceAction.UserAccountElevateRole] = Roles(RoleType.QualityAdmin),
            [ResourceAction.CorpusIngest] = Roles(RoleType.QualityAdmin)
        };

    public static bool IsAuthorized(RoleType role, ResourceAction action) =>
        Default.TryGetValue(action, out var authorizedRoles) && authorizedRoles.Contains(role);
}
```

Lecture de cette matrice : c'est un dictionnaire fermé `Action → ensemble de rôles autorisés`. Une
action absente du dictionnaire (`TryGetValue` échoue) retourne `false` — **default-deny**, pas
default-allow. Ajouter une nouvelle capacité au système exige une entrée explicite ici ; l'oublier
signifie que personne ne peut l'utiliser (erreur silencieuse mais jamais une faille de sécurité).

Et le code réel de `DepartmentScopeGuard` (`src/Agirh.Core/Security/DepartmentScopeGuard.cs`) — la vérification de
portée qui complète cette matrice :

```csharp
public static class DepartmentScopeGuard
{
    public static bool CanAccessDepartment(UserAccount actor, Guid targetDepartmentId) =>
        actor.Role switch
        {
            RoleType.QualityAdmin => true,
            RoleType.HR => actor.DepartmentId == targetDepartmentId,
            _ => false
        };

    public static bool CanAccessEmployee(UserAccount actor, Employee target) =>
        actor.Role switch
        {
            RoleType.QualityAdmin => true,
            RoleType.HR => actor.DepartmentId == target.DepartmentId,
            RoleType.Employee => actor.Id == target.UserAccountId,
            _ => false
        };
}
```

> **Règle métier à retenir** : un `RoleType.Employee` n'apparaît **jamais** dans
> `CanAccessDepartment` (il retombe sur le `_ => false` par défaut) — un collaborateur n'a de portée
> que sur ses propres données (`CanAccessEmployee`), jamais sur un pôle entier. C'est la
> traduction directe en code de "Employee = ses propres données uniquement" : pas une phrase
> de documentation qu'on espère vraie, une expression du switch qu'on peut lire et tester.

Pourquoi pas un système d'identité externe plus riche (Keycloak, Auth0...) ? La décision a été
prise consciemment de ne pas partir sur Keycloak (envisagé puis abandonné) : à l'échelle réelle du
projet (environ 5 RH, 2 Admin/Qualité, et des collaborateurs), la complexité opérationnelle d'un
serveur d'identité dédié (déploiement, configuration, maintenance) dépasse largement la valeur
apportée par rapport à JWT + ASP.NET Identity, qui couvrent le besoin directement. C'est un
exemple concret du principe général : la complexité d'une solution doit être proportionnée à
l'échelle réelle du problème, pas à l'échelle "idéale" ou "à la mode".

## JWT : une identité qui voyage sans état côté serveur

Un **JWT** (JSON Web Token) est une chaîne de caractères encodée en trois parties séparées par des
points : un en-tête (l'algorithme de signature utilisé), une charge utile ou "payload" (les
informations sur l'utilisateur — ici, l'identifiant du compte, le rôle, éventuellement le pôle),
et une signature cryptographique qui garantit que le contenu n'a pas été modifié depuis son
émission par le serveur.

La propriété clé d'un JWT est qu'il est **auto-porteur** ("stateless") : le serveur qui le vérifie
n'a pas besoin de consulter une base de données ou un cache pour savoir qui est l'utilisateur — il
lui suffit de vérifier la signature avec sa clé secrète (`Jwt__SigningKey` dans AGIRH). C'est
l'opposé d'une authentification par session, où le serveur garde en mémoire (ou en base) un
identifiant de session associé à l'utilisateur, et doit interroger ce stockage à chaque requête.

Le compromis à connaître : un JWT signé est valide jusqu'à son expiration (`TokenLifetimeMinutes`,
60 minutes dans AGIRH) même si le compte est entre-temps désactivé côté serveur — contrairement à
une session, qu'on peut invalider instantanément en la supprimant du stockage serveur. C'est
pourquoi la durée de validité est un paramètre de sécurité important : plus elle est courte, plus
vite une révocation de compte devient effective, mais plus les utilisateurs doivent se
reconnecter souvent.

**Code réel — la validation JWT côté Api** (`src/Agirh.Api/Program.cs`) :

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = jwtOptions.Issuer,
    ValidateAudience = true,
    ValidAudience = jwtOptions.Audience,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
    ClockSkew = TimeSpan.FromMinutes(1)
};
```

> **Règle métier/sécurité à retenir** : les **quatre** `Validate*` sont explicitement à `true` — ce
> n'est jamais le cas par défaut dans un exemple copié-collé depuis internet, où `ValidateIssuer`/
> `ValidateAudience` sont souvent laissés à `false` "pour aller plus vite". Chaque `Validate*` à
> `false` est une vérification de sécurité désactivée, silencieusement. `ClockSkew` à 1 minute
> (au lieu du défaut .NET de 5 minutes) réduit aussi la fenêtre de tolérance sur un token expiré.

## Le pattern BFF (Backend For Frontend) : pourquoi le navigateur ne voit jamais le JWT

Le piège classique d'une application web avec authentification par token : si le token est stocké
côté navigateur de façon accessible en JavaScript (`localStorage`, ou un cookie non protégé), la
moindre faille de type **XSS** (Cross-Site Scripting — un script malveillant injecté dans la page,
via un champ de saisie mal filtré ou une dépendance frontend compromise) permet de voler ce token
et d'usurper l'identité de l'utilisateur.

Le pattern **BFF** contourne ce risque structurellement : le frontend Next.js d'AGIRH ne parle
jamais directement à `Agirh.Api` depuis le navigateur. Le navigateur appelle des routes internes à
Next.js (`app/api/auth/login`, `app/api/chat/ask`...), qui elles-mêmes, **côté serveur**
(jamais exécutées dans le navigateur), appellent `Agirh.Api` avec le JWT en en-tête, puis posent
ce JWT dans un **cookie httpOnly**. Un cookie httpOnly est explicitement inaccessible depuis
JavaScript (`document.cookie` ne le voit pas) — même un script injecté par XSS ne peut pas le
lire. Le navigateur continue de l'envoyer automatiquement à chaque requête (c'est le comportement
natif des cookies), mais aucun code JavaScript, légitime ou malveillant, n'y a accès en lecture.

Le coût de ce pattern : chaque appel réseau depuis le navigateur fait un aller-retour
supplémentaire (navigateur → Next.js → Agirh.Api, au lieu de navigateur → Agirh.Api directement),
et Next.js doit maintenir cette couche de proxy pour chaque route utilisée. C'est un coût de
latence et de code accepté en échange d'une réduction réelle de surface d'attaque — un choix
classique en sécurité applicative : accepter un coût mesurable pour éliminer une classe entière de
vulnérabilités, plutôt que de compter sur des mitigations partielles.

**Code réel — la pose du cookie côté serveur Next.js** (`frontend/lib/api/session.ts`) :

```typescript
export async function definirSession(token: string): Promise<void> {
  const store = await cookies();
  store.set(NOM_COOKIE, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: DUREE_COOKIE_SECONDES,
  });
}
```

> **Règle métier/sécurité à retenir** : `httpOnly: true` est la ligne qui porte toute la protection
> contre le vol de token par XSS — sans elle, tout le reste du pattern BFF serait décoratif. Ce
> fichier (`session.ts`) est le **seul** endroit de tout le frontend qui manipule le token
> directement ; aucun composant React, aucun code exécuté dans le navigateur, ne le voit jamais.

## Synthèse : ce que ces trois notions ont en commun

Architecture hexagonale, RBAC à portée, et BFF partagent un même principe de conception : **isoler
une préoccupation critique dans un endroit unique et bien défini**, plutôt que de la disperser. Le
métier est isolé de la technique (hexagonal), la portée d'accès est vérifiée à un seul endroit
avant toute action (`DepartmentScopeGuard`), et le secret d'authentification ne vit qu'à un seul endroit
du système (le serveur Next.js, jamais le navigateur). C'est un fil conducteur qu'on retrouve
aussi dans le pipeline IA et l'orchestration conversationnelle (documents 2 et 3) : centraliser un
risque pour pouvoir le contrôler, plutôt que de faire confiance à chaque point d'usage
individuellement.
