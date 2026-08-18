# Dockerisation & déploiement : comment AGIRH tourne partout de la même façon

*Document autonome. Il couvre la conteneurisation d'AGIRH — pourquoi elle existe, comment chaque
service est construit et orchestré, et une décision de conception volontairement à contre-courant
(ne PAS conteneuriser un des composants) qui illustre bien que Docker n'est pas une solution
universelle à appliquer partout par réflexe.*

## Le problème que la conteneurisation résout

Le symptôme classique, connu de quiconque a développé en équipe : "ça marche sur ma machine".
Une application dépend d'un environnement précis — version du runtime (.NET 8, Node 20), variables
d'environnement, ports disponibles, bibliothèques système installées. Sans mécanisme pour figer
cet environnement, chaque machine (poste de développeur, serveur de démo, machine du jury) risque
de se comporter différemment, pour des raisons souvent difficiles à diagnostiquer.

**Docker** répond à ce problème en empaquetant une application avec *tout* son environnement
d'exécution (runtime, dépendances, configuration) dans une unité portable appelée **image**. Une
**image** est un plan figé, en lecture seule ; un **conteneur** est une instance en cours
d'exécution de cette image. Techniquement, un conteneur n'est pas une machine virtuelle complète :
il partage le noyau du système d'exploitation hôte, mais isole les processus, le réseau et le
système de fichiers grâce à des mécanismes du noyau Linux (namespaces pour l'isolation, cgroups
pour la limitation des ressources). C'est ce qui rend un conteneur nettement plus léger et rapide à
démarrer qu'une machine virtuelle, tout en gardant une isolation forte.

## Docker Compose : orchestrer plusieurs conteneurs comme une seule unité

Une application réelle est rarement un seul processus isolé — AGIRH a besoin d'une base de données
relationnelle, d'une base vectorielle, d'un backend, et d'un frontend, qui doivent démarrer dans le
bon ordre et pouvoir communiquer entre eux. **Docker Compose** décrit cet ensemble de services dans
un seul fichier déclaratif (`docker-compose.yml`), et permet de démarrer toute la pile avec une
seule commande (`docker compose up -d --build`).

AGIRH déclare 4 services :

```yaml
sqlserver  → base relationnelle (comptes, pôles, workflows, checklists)
qdrant     → index vectoriel du pipeline RAG
api        → backend .NET (construit depuis src/Agirh.Api/Dockerfile)
frontend   → Next.js (construit depuis frontend/Dockerfile)
```

### Le réseau interne entre conteneurs

Par défaut, Docker Compose crée un réseau virtuel partagé entre tous les services d'un même
fichier. À l'intérieur de ce réseau, chaque service peut joindre les autres **par leur nom de
service**, résolu automatiquement comme un nom d'hôte — c'est du DNS interne fourni gratuitement
par Docker. Concrètement, dans la configuration de l'`api`, la chaîne de connexion à la base
pointe vers `Server=sqlserver,1433` (le nom du service, pas une adresse IP), et le frontend
contacte l'API via `AGIRH_API_URL=http://api:8080`. Ce mécanisme évite d'avoir à connaître ou
câbler en dur des adresses IP, qui de toute façon changeraient à chaque redémarrage des
conteneurs.

### `depends_on` avec condition : éviter les race conditions au démarrage

Un piège courant en orchestration multi-conteneurs : `depends_on` seul garantit uniquement l'ordre
de **démarrage** d'un conteneur, pas que le service à l'intérieur soit réellement **prêt** à
recevoir des connexions. SQL Server, par exemple, démarre son conteneur presque instantanément,
mais met un temps notable à initialiser sa base et accepter des connexions. Si l'`api` tentait de
se connecter immédiatement, elle échouerait — pas parce que la configuration est fausse, mais
parce que le timing est mauvais.

AGIRH utilise `depends_on` avec une **condition de santé** (`condition: service_healthy`), couplée
à un **healthcheck** défini sur le service `sqlserver` : une commande (`sqlcmd -Q 'SELECT 1'`)
exécutée périodiquement à l'intérieur du conteneur pour vérifier que le service répond vraiment,
pas seulement que son processus est lancé. L'`api` n'est démarrée par Compose qu'une fois ce
healthcheck passé avec succès. C'est une différence importante à bien comprendre entre "le
conteneur tourne" et "le service à l'intérieur est opérationnel" — deux choses souvent confondues
mais distinctes.

## Le Dockerfile : comment une image est construite, étape par étape

Un **Dockerfile** est une suite d'instructions qui décrit comment construire une image, couche par
couche. Chaque instruction (`COPY`, `RUN`...) crée une nouvelle couche, mise en cache par Docker :
si rien n'a changé depuis la dernière construction pour une instruction donnée, Docker réutilise le
résultat déjà construit au lieu de tout refaire — ce qui accélère fortement les reconstructions
répétées pendant le développement.

### Multi-stage build : séparer "construire" de "faire tourner"

Les deux Dockerfiles d'AGIRH (backend et frontend) utilisent la technique du **build multi-étapes**
(multi-stage build) : plusieurs étapes `FROM ... AS <nom>` dans un même fichier, où seule la
dernière étape produit l'image finale réellement déployée.

Pour le backend .NET :

```
Étape "build" (image mcr.microsoft.com/dotnet/sdk:8.0, lourde — contient le compilateur, les
  outils de build, le SDK complet) → compile et publie l'application.
Étape "runtime" (image mcr.microsoft.com/dotnet/aspnet:8.0, nettement plus légère — contient
  seulement ce qu'il faut pour exécuter une application .NET déjà compilée, pas pour la
  compiler) → ne récupère que le résultat déjà compilé de l'étape précédente
  (COPY --from=build /app/publish .).
```

L'intérêt : le SDK complet (compilateur, outils, packages de développement) n'a **aucune raison**
d'exister dans l'image qui tourne réellement en production — il ne sert qu'à la phase de
construction. Le séparer réduit fortement la taille de l'image finale (moins de surface d'attaque,
transferts réseau plus rapides) sans rien retirer de la capacité à exécuter l'application. C'est le
même principe pour le frontend Next.js, avec trois étapes cette fois (`deps`, `builder`,
`runner`) : installer les dépendances npm, construire l'application (`npm run build`), puis ne
garder dans l'image finale que le strict nécessaire à l'exécution (`npm run start`), pas les outils
de build ni les dépendances de développement.

### L'ordre des instructions `COPY` : optimiser le cache

Un détail visible dans le Dockerfile backend, révélateur d'une bonne pratique générale : les
fichiers `.csproj` (qui listent les dépendances NuGet) sont copiés et restaurés (`dotnet restore`)
**avant** de copier le reste du code source. Pourquoi cet ordre précis : la restauration des
dépendances est l'étape la plus lente de la construction, mais elle ne change que rarement (les
dépendances changent bien moins souvent que le code applicatif). En copiant d'abord uniquement les
fichiers de dépendances, Docker peut réutiliser la couche de cache du `restore` tant que ces
fichiers ne changent pas — même si le code source, lui, change à chaque modification. Inverser cet
ordre (copier tout le code d'abord) invaliderait le cache du restore à chaque changement de code,
même le plus minime, et re-téléchargerait toutes les dépendances à chaque reconstruction —
beaucoup plus lent en développement itératif.

## Volumes : ce qui doit survivre au-delà du conteneur

Un conteneur est **éphémère** par nature : son système de fichiers interne est perdu quand il est
supprimé et recréé. C'est un problème pour tout ce qui doit persister — les données d'une base,
notamment. Docker répond à ça avec des **volumes**, des emplacements de stockage gérés par Docker,
existant indépendamment du cycle de vie d'un conteneur particulier.

AGIRH déclare deux volumes nommés persistants (`agirh-sql-data`, `agirh-qdrant-data`), attachés
respectivement aux répertoires de données internes de SQL Server et de Qdrant — supprimer et
recréer ces conteneurs (par exemple lors d'une mise à jour d'image) ne perd pas les données
stockées.

Un second usage des volumes, différent, apparaît sur le service `api` : les répertoires `rag/models/`
(les poids ONNX du pipeline RAG, environ 850 Mo) et `rag/corpus/` (les documents source) sont montés en
**lecture seule** (`:ro`) depuis l'hôte, plutôt que copiés à l'intérieur de l'image au moment de la
construction. C'est un **bind mount** (un dossier réel de la machine hôte, monté tel quel dans le
conteneur), différent d'un volume nommé géré par Docker. Le choix ici : ces fichiers sont trop
volumineux et changent trop indépendamment du code applicatif pour justifier d'alourdir l'image à
chaque construction — les monter depuis l'hôte permet de les mettre à jour sans reconstruire
l'image, et de partager le même téléchargement de modèles entre plusieurs contextes (dev local,
conteneur) sans dupliquer 850 Mo à chaque fois.

## La décision volontaire de NE PAS conteneuriser Ollama

C'est le point de conception le plus instructif de ce document, parce qu'il illustre qu'appliquer
Docker "à tout" par principe n'est pas toujours le bon réflexe. Ollama (le moteur d'inférence LLM)
tourne **nativement sur la machine hôte**, pas dans un conteneur Compose, et c'est une décision
**explicite**, pas un oubli :

- Ollama était **déjà installé et déjà utilisé** en développement avant même la dockerisation du
  reste du projet — le reconteneuriser n'aurait apporté aucune valeur immédiate.
- Le porteur du projet a accès, via son stage, à un **second serveur Ollama distant**, sur le
  réseau de son entreprise, avec des modèles plus capables. Le nom de modèle (pas seulement
  l'URL) est configurable indépendamment (`Ollama:RouterModel`/`Ollama:GeneratorModel`) pour
  basculer entre les deux profils sans recompiler — une flexibilité plus simple à gérer avec un
  Ollama natif qu'avec un Ollama conteneurisé supplémentaire à synchroniser.
- Conteneuriser une charge de calcul lourde comme l'inférence LLM introduit une complexité
  additionnelle réelle si un GPU doit être exposé au conteneur (passthrough GPU), sans bénéfice
  clair ici puisque la machine de développement n'a de toute façon pas de GPU adapté pour cet
  usage.

Le conteneur `api`, lui, doit quand même joindre cet Ollama natif depuis l'intérieur de son réseau
isolé. C'est le rôle de `host.docker.internal` — un nom d'hôte spécial fourni par Docker qui se
résout vers la machine hôte elle-même, permettant à un conteneur de "sortir" du réseau Compose pour
atteindre un service tournant directement sur la machine physique/virtuelle qui héberge Docker.
`extra_hosts: host.docker.internal:host-gateway` dans la configuration de l'`api` assure que cette
résolution fonctionne aussi sous Docker Engine Linux sans Docker Desktop, où elle n'est pas
automatique par défaut (sur Docker Desktop Windows/Mac, elle l'est nativement) — une ligne de
portabilité, sans effet quand elle n'est pas nécessaire.

## Secrets et configuration

Deux informations sensibles sont nécessaires au démarrage : le mot de passe administrateur SQL
Server (`SQL_SA_PASSWORD`) et la clé de signature JWT (`JWT_SIGNING_KEY`). Elles ne sont **jamais**
écrites en dur dans `docker-compose.yml` ni commitées dans le dépôt Git — elles sont lues depuis un
fichier `.env` local (copié depuis un `.env.example` versionné, qui documente la structure attendue
sans exposer de vraie valeur), explicitement exclu du contrôle de version. C'est le mécanisme
standard pour séparer *la structure* d'une configuration (versionnée, partagée) de *ses valeurs
sensibles* (locales, jamais partagées).

## Migrations automatiques : réduire les étapes manuelles au démarrage

Un piège classique du déploiement conteneurisé : une base de données fraîchement créée (premier
démarrage sur un volume vide) n'a pas encore le schéma attendu par l'application. AGIRH applique
les migrations EF Core (`dbContext.Database.Migrate()`) **automatiquement au démarrage** de
l'`api`, aussi bien en développement local qu'en conteneur — code réel (`src/Agirh.Api/Program.cs`) :

```csharp
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AgirhDbContext>().Database.Migrate();
}
``` C'est une opération **idempotente** —
l'exécuter sur une base déjà à jour ne fait rien, l'exécuter sur une base vide ou en retard
applique exactement ce qu'il faut. Ce choix élimine une étape manuelle (`dotnet ef database
update`) qui serait facile à oublier, en particulier sur un environnement de démo où on ne veut pas
dépendre d'une checklist de setup manuelle fragile.

## Forces et limites de l'approche Docker Compose ici

**Forces** : reproductibilité (la même pile démarre à l'identique sur n'importe quelle machine avec
Docker installé), une seule commande pour tout démarrer, isolation propre entre les services,
adapté exactement à l'usage visé — une démo/soutenance locale, pas un déploiement à grande échelle.

**Limites, à ne pas perdre de vue** : Docker Compose n'est pas un outil d'orchestration de
production à grande échelle (pas de répartition de charge automatique, pas de haute disponibilité,
pas de redémarrage intelligent multi-machines — c'est le rôle d'outils comme Kubernetes, hors de
propos ici). Le passthrough GPU reste complexe à gérer proprement en conteneur, ce qui a
directement motivé la décision de garder Ollama natif. Et la dockerisation ne résout rien à la
capacité réelle du modèle LLM exécuté (document 03) — elle ne fait que garantir que
l'*environnement* autour de ce modèle est reproductible, pas que le modèle lui-même sera plus
rapide ou plus précis.
