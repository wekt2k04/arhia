# AGIRH — Assistant RH agentique

Agent conversationnel d'onboarding et d'offboarding, avec pipeline RAG (Retrieval-Augmented
Generation) et orchestration LLM locale. Projet de fin d'études (stage été 2026) : conception
fonctionnelle, architecture technique, pipeline IA et sécurité des accès, jusqu'à un prototype
opérationnel démontrable de bout en bout.

## Aperçu

- **Interface agent-first** : page de garde, connexion/inscription, puis un chat conversationnel
  comme point d'entrée principal — pas de dashboard séparé.
- **Réponses sourcées** : chaque réponse documentaire cite les documents dont elle est extraite ;
  l'agent refuse explicitement plutôt que d'inventer quand rien de pertinent n'est trouvé
  (anti-hallucination encodée en code, pas seulement dans le prompt).
- **Strictement informatif** : aucune action destructrice ou d'écriture n'est jamais déclenchée
  par l'agent — lecture seule sur les données de statut, écritures réservées aux endpoints
  métier classiques avec contrôle RBAC.
- **RBAC à 3 rôles avec portée** : Collaborateur (ses données), RH (son pôle uniquement), Admin/
  Qualité (portée globale, circuit de validation des templates).
- **Temps réel** : chat en streaming token par token et barre de notifications, tous deux via
  Server-Sent Events (SSE), à travers un pattern BFF qui ne laisse jamais le token
  d'authentification atteindre le navigateur en clair.

## Architecture

Architecture hexagonale (ports & adaptateurs) — dépendance à sens unique, jamais vers le haut :

```mermaid
graph TB
    Domain["Agirh.Domain<br/>entités pures"]
    Core["Agirh.Core<br/>ports, use cases, RBAC"]
    Infra["Agirh.Infrastructure<br/>EF Core, Qdrant, ONNX, Ollama, SSE"]
    Api["Agirh.Api<br/>Controllers, composition root"]
    Front["frontend/<br/>Next.js (BFF)"]

    Core --> Domain
    Infra --> Core
    Infra --> Domain
    Api --> Infra
    Api --> Core
    Api --> Domain
    Front -.HTTP/SSE.-> Api
```

Détail complet, diagrammes de flux et arborescence cible : [`ARCHITECTURE.md`](ARCHITECTURE.md).

### Pipeline RAG (4 phases, toutes obligatoires)

`Chunking structurel + recouvrement` → `Embedding ONNX multilingue (768d)` → `Recherche Qdrant
(ANN/HNSW, cosinus)` → `Reranking cross-encoder (BAAI/bge-reranker-v2-m3)`

### Orchestration conversationnelle

`Router (Ollama, phi4-mini:3.8b)` classe l'intention (documentaire / statut de dossier / hors
périmètre) → `Generator (Ollama)` écrit la réponse, sourcée si le RAG a été utilisé. Toute sortie
du Router qui ne matche pas exactement l'un des trois cas retombe par défaut sur hors périmètre
(fail-safe, pas fail-open).

## Stack technique

| Composant | Choix |
|---|---|
| Backend | .NET 8, ASP.NET Core, EF Core |
| Frontend | Next.js 15 (BFF, cookie httpOnly), TailwindCSS, shadcn/ui (Radix) |
| Auth | JWT + ASP.NET Identity |
| Temps réel | SSE (chat + notifications) |
| Base relationnelle | SQL Server |
| Base vectorielle | Qdrant (ANN/HNSW, cosinus) |
| Embedding / Reranking | ONNX Runtime .NET (modèle multilingue / cross-encoder) |
| LLM | Ollama local (`phi4-mini:3.8b`) |
| Déploiement démo | Docker Compose |
| Tests | xUnit, Moq, FluentAssertions |

Détail et justification de chaque choix : [`STACK_TECHNIQUE.md`](STACK_TECHNIQUE.md).

## Démarrage rapide

### Prérequis

- .NET 8 SDK, Node 20+ (18.20 minimum pour le développement local hors Docker), Docker.
- [Ollama](https://ollama.com) installé nativement avec `phi4-mini:3.8b` disponible
  (`ollama pull phi4-mini:3.8b`) — non conteneurisé, voir `STACK_TECHNIQUE.md` §2.

### Option A — Docker Compose (recommandé pour une démo)

```bash
cp .env.example .env               # renseigner SQL_SA_PASSWORD et JWT_SIGNING_KEY
docker compose up -d --build       # sqlserver + qdrant + api + frontend
```

Au tout premier démarrage sur une base fraîche : promouvoir un compte en `AdminQualite` en SQL,
puis `POST /api/admin/reindexer-corpus` pour peupler Qdrant — sans ça le chat documentaire ne
trouvera rien. Frontend sur `http://localhost:3000`, Api sur `http://localhost:5080`.

### Option B — développement local, sans Docker pour l'Api/le frontend

```bash
docker start agirh-sql agirh-qdrant     # ou les créer si absents (SQL Server + Qdrant)
ollama serve                            # avec phi4-mini:3.8b déjà tiré

cd src/Agirh.Api && dotnet run -c Release      # port 5080
cd frontend && npm run dev                      # port 3000, voir .env.example
```

### Build & tests

```bash
dotnet build Agirh.sln -c Release
dotnet test Agirh.sln -c Release
```

## Structure du dépôt

```
src/
  Agirh.Domain/          entités métier pures, zéro dépendance externe
  Agirh.Core/             ports, use cases, RBAC (RbacMatrix, PoleScopeGuard)
  Agirh.Infrastructure/   adaptateurs : EF Core, Qdrant, ONNX (RAG), Ollama (LLM), SSE
  Agirh.Api/              Controllers, composition root (Program.cs)
frontend/                 Next.js — BFF, chat, notifications, pages d'auth
corpus/                   corpus source du pipeline RAG (documents Markdown)
tests/Agirh.Tests/        xUnit + Moq + FluentAssertions
docs/notebooklm/          synthèses approfondies (architecture, IA, Docker, workflows)
APPRENTISSAGE/            notes de montée en compétence personnelle
HANDOFF/                  protocole de continuité entre sessions de travail
```

## Documentation

| Document | Contenu |
|---|---|
| [`LOGIQUE_METIER.md`](LOGIQUE_METIER.md) | Rôles, workflows métier, RBAC, garde-fous IA |
| [`STACK_TECHNIQUE.md`](STACK_TECHNIQUE.md) | Stack backend/frontend/données/IA et justifications |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Détail hexagonal, arborescence, diagrammes de flux |
| [`CHECKLIST.md`](CHECKLIST.md) | Suivi milestone par milestone, statut réel |
| [`HISTORIQUE.md`](HISTORIQUE.md) | Pourquoi le projet a été reconstruit de zéro (V7→V8) |
| [`docs/notebooklm/`](docs/notebooklm/) | Approfondissements (architecture, RAG, orchestration IA, Docker, workflows) |

## Statut du projet

Milestones 0-9 et 11 terminés — application fonctionnelle de bout en bout, y compris en Docker
Compose sur base fraîche : inscription/connexion, chat en streaming réel, notifications en
direct. Restent ouverts : l'affinage du routeur conversationnel (~27% de mauvais routage mesuré,
mis de côté volontairement), la reconfirmation du jeu de questions/réponses de référence, et
quelques endpoints de lecture annexes. Détail exhaustif dans [`CHECKLIST.md`](CHECKLIST.md).
