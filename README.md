# AGIRH — Assistant RH Agentique

> **Assistant RH intelligent** : chat IA (RAG + RBAC), congés, avance sur salaire, onboarding — **.NET 8 hexagonal + Next.js 15**.
> **État :** backend **122/122 tests** · build 0 erreur · audit V5 **GO** + remédiation modèle **R1-R8** (voir `AGIRH_MODEL_AUDIT.md`) · frontend Next.js (103→206 kB First Load).

---

## 1. Fonctionnalités

| Module | Détail |
|---|---|
| 💬 **Chat agentique** | Pipeline **6 acteurs** (Profiler → PreFlight → Dispatcher RBAC → Worker MAF → Synthesizer → **Checker fail-closed**), streaming SSE ~30 ms/token ; salutations détectées par le profiler (LLM) puis routées GeneralChat → synthétiseur → checker |
| 🔒 **Sécurité** | JWT HS256 + BCrypt, **FallbackPolicy secure-by-default**, RBAC fail-closed, rate-limit (429), anti-énumération (404 seul), **TOCTOU** (index unique filtré) |
| 🌐 **RAG** | Base de connaissances RH, embeddings 768d `vector(768)`, recherche `VECTOR_DISTANCE` top-5 |
| 📅 **Congés & CET** | Solde, historique, **pose de demande (Pending)**, approbation manager (subordonnés uniquement) |
| 💶 **Avance sur salaire** | Cap 50 % du net, un seul `Pending`/employé, carte widget `||WIDGET:SalaryAdvance:{id}||` |
| 🧾 **Onboarding** | Checklist générée par catégorie (Admin\|Manager) |
| 🖥️ **Frontend** | Next.js 15 App Router + Tailwind, JWT en cookie httpOnly (BFF), Markdown sanitizé, mobile-first (drawer) |

## 2. Architecture

```
Domain (pur) → Core (ports + use cases) → Infrastructure (adaptateurs) → Api (composition root)
                                        → frontend/ (Next.js, découplé par BFF)
```

- **Pipeline chat** : `AgentController(SSE) → AgentOrchestratorService → Profiler → PreFlightValidator → ZeroTrustDispatcher(RbacMatrix) → Worker(MAF) → Synthesizer → Checker → marqueurs → Token Replay`.
- **Sécurité** : RBAC source unique `RbacMatrix.Default` ; déni RBAC → événement SSE `denied` (bulle orange).
- **Base** : SQL Server 2025, 5 migrations EF, index unique filtré anti-TOCTOU.

## 3. Stack

| Couche | Technologie |
|---|---|
| Backend | .NET 8 · ASP.NET Core · EF Core 8 · Polly |
| IA | Ollama (`phi4-mini:3.8b`, `qwen3.5:9b`, `embeddinggemma`) |
| Frontend | Next.js 15.5.22 · TypeScript · TailwindCSS · react-markdown |
| Base | SQL Server 2025 (Docker) · EF Core InMemory (tests) |
| Tests | xUnit · FluentAssertions · Moq — **122/122** |

## 4. Prérequis

| Composant | Exigence |
|---|---|
| .NET SDK | ≥ 8.0 |
| Node.js | ≥ 18 (Next 15.5.22) |
| Docker | SQL Server 2025 (`agirh-sql:1433`) |
| Ollama | `192.168.100.220:11434` (ou votre serveur) — modèles : `phi4-mini:3.8b`, `qwen3.5:9b`, `embeddinggemma` |

## 5. Démarrage rapide

> **⚠️ 2 profils de lancement** (`src/Agirh.Api/Properties/launchSettings.json`) — ils ne diffèrent QUE par l'environnement et donc l'endpoint Ollama :
>
> | Profil | `ASPNETCORE_ENVIRONMENT` | Config chargée | Ollama | Modèles |
> |---|---|---|---|---|
> | **Agirh_Bureau** | `Development` | `appsettings.json` + `appsettings.Development.json` | entreprise `192.168.100.220:11434` | `phi4-mini:3.8b` · `qwen3.5:9b` · `embeddinggemma` |
> | **Agirh_Maison** | `local` | `appsettings.json` + `appsettings.local.json` (gitignoré) | **local** `localhost:11434` | `phi4-mini:3.8b` · `gemma4:12b` · `embeddinggemma` (768d) |

### Backend

```powershell
docker build -t agirh-sql docker/sql                          # SQL Server 2025
docker run -d --name agirh-sql -p 1433:1433 -e MSSQL_SA_PASSWORD=YourStrong!Passw0rd agirh-sql
cd src\Agirh.Api
dotnet run                      # http://localhost:5000 (migrations + seeds auto)
```

> L'API applique les migrations au démarrage et seed les 4 comptes démo. Les fichiers RAG se trouvent dans `src\Agirh.Api\knowledge_base\` (ingestion : `POST /api/admin/ingest` en Admin).

### Frontend

```powershell
cd frontend
npm install
npm run dev                     # http://localhost:3000
```

> Le frontend communique avec l'API **uniquement via ses BFF** (cookie httpOnly). `AGIRH_API_URL` (défaut `http://localhost:5000`) se configure dans `frontend/.env.local`.

### Comptes démo

| Rôle | Email | Mot de passe |
|---|---|---|
| Admin | `admin@agirh.fr` | `admin123` |
| Manager | `marie.martin@agirh.fr` | `manager123` |
| Collaborateur | `jean.dupont@agirh.fr` | `collab123` |

## 5bis. Ingestion de la base de connaissances (RAG)

> **Résumé** : vectorise `src\Agirh.Api\knowledge_base\*.md` (chunking 512/128 mots, batching 10 + 200 ms). **Le modèle d'embedding doit produire exactement 768 dimensions** (colonne `vector(768)`, **garde fail-fast** dans `OllamaEmbeddingGenerator`). `POST /api/admin/ingest` **appende** — un re-run sans `DELETE` préalable est rejeté par **l'index unique `(SourceFile, ChunkIndex)`** (migration `20260804095319`).

| Prérequis | Exigence |
| --- | --- |
| Modèle d'embedding | **768d** : `embeddinggemma:latest` (défaut) ou `nomic-embed-text:latest` |
| Serveur Ollama | `AI:Endpoint` (ex. `http://192.168.100.220:11434`) — joignable |
| Jeton | JWT d'`admin@agirh.fr` |
| Corpus | `*.md` dans `src\Agirh.Api\knowledge_base\` |

> **Piège dimension** : `Embedding:ExpectedDimension` (défaut 768) est **lu et vérifié** (garde fail-fast) par `OllamaEmbeddingGenerator` (`OllamaEmbeddingGenerator.cs:21,57-60`) — un embedding hors 768d **fait échouer l'ingestion**. Modèles compatibles : `embeddinggemma`/`nomic-embed-text` = 768 ✅ · `all-minilm` = 384 ❌ · `mxbai-embed-large`/`bge-m3` = 1024 ❌. (Le profil `local` utilise `appsettings.local.json`, gitignoré, pour l'Ollama de la maison.)

**Séquence de re-ingestion** :
1. Lister les modèles disponibles : `curl.exe http://192.168.100.220:11434/api/tags`
2. Configurer `AI:Endpoint` + `AI:EmbeddingModel` (768d) puis **redémarrer l'API**
3. Login Admin + **vider la base** : `curl.exe -X DELETE http://localhost:5000/api/admin/ingest -H "Authorization: Bearer $TOKEN"`
4. **Ré-ingérer** : `curl.exe -X POST http://localhost:5000/api/admin/ingest -H "Authorization: Bearer $TOKEN"`
5. **Vérifier** (voir tableau)

| Vérification | Attendu |
| --- | --- |
| Logs `logs\agirh-api.log` | `Ingested <fichier>: N chunks`, aucun `Erreur de persistence` |
| Base | `SELECT COUNT(*) FROM KnowledgeDocuments;` → **> 0** (commit explicite) |
| Dimension | `SELECT TOP(1) JSON_LENGTH(Embedding) FROM KnowledgeDocuments;` → `768` |
| Test RAG | Chat « Quelles sont les règles de télétravail ? » → réponse sourcée depuis la base |

> Détail complet + dépannage : `AGIRH_SIMULATION_RUNBOOK.md` (étape A4).

## 6. Tests

```powershell
dotnet build Agirh.sln -c Release
dotnet test -c Release          # 122/122 (backend)
cd frontend; npm run build      # 0 erreur (frontend)
```

Suite manuelle E2E : `tests.http` (VS Code REST Client) et `AGIRH_SIMULATION_RUNBOOK.md`.

## 7. Structure du projet

```
Agirh.sln · docker/sql/Dockerfile · tests.http · AGIRH_SIMULATION_RUNBOOK.md
├── src/
│   ├── Agirh.Domain/            # Entités pures (0 dépendance)
│   ├── Agirh.Core/              # Ports, RbacMatrix, use cases
│   ├── Agirh.Infrastructure/    # EF, repos, agents IA, MAF, outils
│   └── Agirh.Api/               # Contrôleurs, Dtos, Program.cs (DI, sécurité)
├── tests/Agirh.Tests/           # 122 tests (pipeline, RBAC, SSE, TOCTOU, R1-R8)
├── frontend/                    # Next.js 15 (BFF, chat, sidebar, widgets)
└── docs/
    ├── notebooklm/              # Base de connaissance projet (étude)
    ├── audits/                  # Certification V5 + audit modèle
    └── presentation/            # Soutenance (pptx, script, assets)
```

## 8. Documentation

| Document | Rôle |
| --- | --- |
| `AGIRH_SIMULATION_RUNBOOK.md` | **Validation E2E** (Partie Back + Front, critères OK/KO) |
| `AGIRH_MODEL_AUDIT.md` | **Audit comportement du modèle + remédiation R1-R8** (IA/RAG) |
| `docs/notebooklm/00_README_INDEX.md` | Carte des notions → code + méthode d'étude |
| `docs/notebooklm/07_BUSINESS_DOMAIN.md` | Logique métier (congés, avance, workflows) |
| `docs/audits/AGIRH_Architecture_Final_Audit_V5.md` | Certification backend V5 |

## 9. Déploiement (état actuel)

**Seul SQL Server est conteneurisé** (`docker/sql/Dockerfile`, image officielle `2025-latest`, port 1433). L'API et le frontend se lancent **hors Docker** (`dotnet run`, `npm run dev`). L'ancienne orchestration `docker-compose` (3 services API + frontend + base) a été **abandonnée** — la configuration actuelle ne comprend qu'une seule image. Secrets injectés par variables d'environnement (jamais dans `appsettings`).
