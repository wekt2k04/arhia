# AGIRH — Vue d'ensemble pour apprentissage

*Document dense, pensé pour être recopié à la main en 1-2h. Chaque notion est expliquée avec le
"pourquoi", pas juste le "quoi" — recopier le pourquoi est ce qui fait retenir. Pour la référence
exhaustive et à jour : `LOGIQUE_METIER.md`, `STACK_TECHNIQUE.md`, `ARCHITECTURE.md`.*

## 1. Le principe central : architecture hexagonale

Le code est rangé en 4 couches, avec **une règle unique** : une couche ne dépend que de celles
listées en dessous d'elle, jamais l'inverse.

```
Agirh.Domain          (entités pures : Collaborateur, Pole, WorkflowTemplate...)
   ↑
Agirh.Core            (ports = interfaces, use cases, RBAC)
   ↑
Agirh.Infrastructure  (un adaptateur par port : EF Core, Qdrant, ONNX, Ollama, SSE)
   ↑
Agirh.Api             (Controllers, Program.cs = composition root)
```

**Pourquoi ce sens précis ?** Domain ne sait même pas qu'une base de données existe. Core définit
*ce dont il a besoin* sous forme d'interface (`IWorkflowInstanceRepository`, `ILlmRouterPort`...)
sans savoir comment c'est fait. Infrastructure fournit l'implémentation concrète (EF Core pour le
port repository, Ollama pour le port LLM). Résultat concret : remplacer SQL Server par autre chose,
ou Ollama par une vraie API cloud, ne touche *que* Infrastructure — Core et Domain ne bougent pas.
C'est le pattern **ports & adaptateurs** : le port est le contrat (interface), l'adaptateur est
l'implémentation qui le respecte.

`Agirh.Api` est la **composition root** : c'est le seul endroit qui choisit *quel* adaptateur
concret injecter derrière chaque port (`Program.cs`, `builder.Services.AddScoped<...>()`). Les
Controllers eux-mêmes n'ont aucune logique métier — ils appellent un use case de Core et
retournent le résultat.

## 2. RBAC : qui a le droit de faire quoi

3 rôles, portée croissante :
- **Collaborateur** — voit uniquement ses propres données.
- **RH** — gère les collaborateurs de **son pôle uniquement**. Vérifié par `PoleScopeGuard`,
  *avant même* la vérification du rôle — un RH qui cible un dossier hors de son pôle est refusé,
  peu importe qu'il ait le bon rôle.
- **AdminQualite** — portée globale, seul rôle qui peut élever un compte (Collaborateur→RH,
  etc.) et valider/approuver des templates.

Pas de Keycloak ni de système d'auth externe — jugé disproportionné pour l'échelle réelle
(~5 RH + 2 Admin/Qualité + collaborateurs). JWT + ASP.NET Identity suffisent.

## 3. Le frontend ne détient jamais le JWT — pattern BFF

Next.js joue le rôle de **Backend For Frontend** : le navigateur ne parle *jamais* directement à
`Agirh.Api`. Il parle à des Route Handlers Next.js (`app/api/auth/login`, `app/api/chat/demander`...)
qui, eux, appellent l'Api côté serveur avec le JWT, et posent un **cookie httpOnly** — invisible et
inaccessible en JavaScript côté client. Pourquoi : si un JWT était stocké en `localStorage` ou
exposé au JS client, une faille XSS suffirait à le voler. Avec httpOnly, même un script injecté ne
peut pas le lire.

## 4. Temps réel : SSE, un seul mécanisme pour deux usages

**Server-Sent Events** — un flux HTTP unidirectionnel serveur→client, plus simple qu'un WebSocket
quand on n'a pas besoin du sens client→serveur en continu (ici, la question part en HTTP normal,
seule la réponse doit arriver en flux). Utilisé pour :
- Le **chat** — la réponse de l'agent arrive token par token (`fragment` events), puis une frame
  `termine` avec les sources.
- Les **notifications** — un tableau JSON complet renvoyé toutes les 10s (pas un delta accumulé ;
  chaque frame remplace la précédente côté client).

**Piège réel rencontré** : la compression HTTP intégrée de Next.js bufferise toute la réponse
avant de l'envoyer — incompatible avec du streaming (rien n'arrive avant la toute fin). Fix :
`compress: false` dans `next.config.ts`.

## 5. Le pipeline RAG — 4 phases, toutes obligatoires

RAG = Retrieval-Augmented Generation : au lieu de laisser le LLM répondre de mémoire (donc halluciner),
on va chercher les passages pertinents dans les vrais documents, et on force le LLM à ne parler que
de ça.

1. **Chunking** — découper les documents Markdown en morceaux, **par structure** (sections/titres)
   et **avec recouvrement** entre chunks voisins (pour ne pas couper une idée en deux sans contexte).
2. **Embedding** — transformer chaque chunk en vecteur (768 dimensions), via un modèle **multilingue**
   (le corpus est en français) tournant en ONNX Runtime pur .NET — pas besoin d'Ollama pour cette
   étape, pipeline entièrement maîtrisé.
3. **Storage** — les vecteurs vivent dans **Qdrant**, indexés en HNSW (Hierarchical Navigable Small
   World) pour une recherche ANN (Approximate Nearest Neighbor) rapide, similarité cosinus.
4. **Reranking** — un **cross-encoder** (`BAAI/bge-reranker-v2-m3`) réordonne les meilleurs candidats
   de Qdrant. Différence clé embedding vs reranking : l'embedding compare requête et document
   *séparément* (rapide, approximatif) ; le cross-encoder les lit *ensemble* (lent mais précis).
   **Piège réel** : un score de reranking élevé (jusqu'à 0.78 observé) ne garantit pas que le chunk
   contient la réponse — juste qu'il est topiquement proche. Le reranking filtre les candidats, il
   ne remplace pas la lecture par le générateur.

## 6. Orchestration conversationnelle — distincte du RAG

Le RAG répond à "où chercher l'info". L'orchestration répond à "quoi faire de la question" :

```
Router (Ollama, phi4-mini:3.8b)  → classe l'intention : DOCUMENTAIRE | STATUT_DOSSIER | HORS_PERIMETRE
Generator (Ollama, même modèle en pratique) → écrit la réponse finale, sourcée si RAG utilisé
```

**Fail-safe, pas fail-open** : la sortie brute du Router n'est jamais utilisée telle quelle. Elle
est validée contre un enum fermé — tout ce qui ne matche pas exactement une des 3 valeurs (sortie
vide, timeout, mot halluciné par le modèle) retombe sur `HORS_PERIMETRE` par défaut. Un flou ou une
panne du LLM ne peut donc jamais accidentellement ouvrir l'accès à quelque chose.

L'agent reste **informatif uniquement** : même pour une question de statut de dossier, le chemin
passe par un port de *lecture seule* — jamais d'écriture déclenchée par le LLM.

**Écart stack assumé** : Router et Generator utilisent le même modèle (`phi4-mini:3.8b`) — l'idée
initiale de 2 modèles distincts (un petit pour classer, un plus gros pour générer) s'est heurtée au
réel : `gemma4:12b` met plus de 2 minutes sans réponse sur cette machine (pas de GPU). Décision
transparente : un compromis technique documenté vaut mieux qu'un choix caché.

## 7. Les deux flux de logs

- **Log technique** — debug/erreurs, pour développer.
- **Audit trail** — trace *métier* : qui a coché quel item, qui a validé/rejeté un template, qui a
  créé/clôturé/archivé un dossier. Exigé par le contexte réel (conformité qualité SMSI) : on doit
  pouvoir répondre "qui a fait quoi, quand" indépendamment du debug technique.

## 8. Trace de flux n°1 — question documentaire dans le chat

1. Le navigateur ouvre un `EventSource` vers `app/api/chat/demander` (Next.js, BFF).
2. Le Route Handler proxy la requête vers `Agirh.Api`, `GET /api/chat/demander?question=...`
   (`ChatController.Demander`), JWT en en-tête.
3. Le use case appelle le **Router** : intention = `DOCUMENTAIRE`.
4. Pipeline RAG : embedding de la question → recherche Qdrant (top-K) → reranking ONNX.
5. Si aucun chunk pertinent (0 candidat, ou tout sous le seuil) → réponse "je n'ai pas trouvé
   cette information", **sans jamais appeler le Generator** (anti-hallucination *en code*, pas
   seulement dans le prompt).
6. Sinon : les chunks rerankés sont passés au **Generator**, qui écrit la réponse en streaming.
7. Chaque fragment part en frame SSE `fragment` ; la frame finale `termine` porte `sourcee` +
   la liste des sources.
8. Le `ChatWidget` (React) accumule les fragments dans le message en cours, affiche les sources
   à la fin.

## 9. Trace de flux n°2 — circuit de validation d'un template

Système à 3 rôles (Rédacteur → Vérificateur → Approbateur), tous portés par `AdminQualite` en
pratique (une même personne peut avoir plusieurs casquettes, mais le *circuit* impose les étapes) :

1. `POST /api/templates` (`TemplateController.Proposer`) — un RH propose une nouvelle version,
   statut `Brouillon`.
2. `POST /api/templates/{id}/verifier` — passage à `EnValidation`/Vérifié.
3. `POST /api/templates/{id}/approuver` — statut final `Approuve`, version figée.
   (`POST /api/templates/{id}/rejeter` existe aussi, renvoie en amont.)
4. **Seul un template `Approuve` peut instancier un `WorkflowInstance`** — un onboarding ne peut
   pas partir d'un brouillon.
5. Chaque changement de statut notifie en SSE le rôle suivant dans le circuit.

## 10. Trace de flux n°3 — onboarding d'un collaborateur

1. `POST /api/workflows` (`WorkflowController.Instancier`) — un RH crée une fiche (Poste, Pôle,
   Contrat, Date).
2. Le use case `ResoudreReferentielItems` calcule la liste d'items attendus, en croisant
   **Poste × Pôle × Contrat** contre le template *approuvé* correspondant.
3. `WorkflowInstance` + `ChecklistItem[]` sont persistés, statut `EnCours`.
4. `POST /api/workflows/{id}/items/{itemId}/cocher` — chaque item se coche indépendamment
   (traçé dans l'audit trail : qui, quand).
5. `POST /api/workflows/{id}/cloturer` puis `.../archiver` — cycle de vie complet, l'archivage
   rend le dossier lecture seule.

## 11. Déploiement — qui tourne où

Docker Compose, 4 services + Ollama natif (jamais conteneurisé, décision explicite du porteur du
projet — accès prévu à un second Ollama sur le réseau entreprise) :

| Service | Port hôte | Rôle |
|---|---|---|
| `sqlserver` | 1433 | Toutes les entités métier relationnelles |
| `qdrant` | 6333 | Index vectoriel RAG |
| `api` | 5080 → 8080 | Backend .NET, migrations EF Core auto-appliquées au démarrage |
| `frontend` | 3000 | Next.js, BFF |
| Ollama | 11434 | Natif sur l'hôte ; l'Api conteneurisée le rejoint via `host.docker.internal` |

Secrets (mot de passe SQL, clé JWT) toujours via `.env`/`appsettings.Development.json`, jamais
commités dans le repo.

## 12. Ce qui reste un chantier ouvert (pour situer l'état réel, pas juste la cible)

- **Routeur** : ~27% de mauvais classement mesuré sur le jeu de Q/R gold — connu, mis de côté
  volontairement (pistes non tentées documentées dans `HANDOFF/NEXT_SESSION.md`).
- **3 cas particuliers métier** (mutation inter-pôle, annulation/suspension, pôle vacant) — pas
  encore de use case dédié, comportement pas encore validé avec le porteur du projet.
- **Endpoints de lecture/liste** (ex. "mes collaborateurs") — n'existent pas encore, seule
  l'écriture était priorisée.
