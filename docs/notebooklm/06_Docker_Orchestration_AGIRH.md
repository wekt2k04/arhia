# AGIRH — Docker & Déploiement (V6 · révisé)

> **Objectif :** maîtriser la conteneurisation actuelle du projet : **une seule image** (SQL Server 2025 via `docker/sql/Dockerfile`), API et frontend lancés **hors Docker**, pont vers Ollama, secrets par variables d'environnement.
> **⚠️ Mise à jour :** l'ancienne orchestration `docker-compose` (3 services : `agirh-sql`, `agirh-api`, `agirh-web`, healthchecks en cascade) a été **abandonnée** et `docker-compose.yml` **supprimé**. Le conteneur porte le nom `agirh-sql` (compatibilité avec le runbook).

---

## 1. Résumé exécutif (3 puces)

- **1 seule image** : SQL Server 2025 (`docker/sql/Dockerfile`, base officielle `mcr.microsoft.com/mssql/server:2025-latest`, port `1433`).
- **API et frontend hors Docker** : `dotnet run` (backend) · `npm run dev` (Next.js). Ollama est joint par son **endpoint direct** (`AI:Endpoint`), pas de pont de réseau Docker nécessaire.
- **Secrets par variables d'environnement** (jamais dans `appsettings` — une image Docker est un fichier : un secret en clair fuirait dans le registre).

---

## 2. La seule image : SQL Server (`docker/sql/Dockerfile`)

| Point | Valeur |
|---|---|
| Base | `mcr.microsoft.com/mssql/server:2025-latest` (support natif `vector(768)` pour le RAG) |
| Env | `ACCEPT_EULA=Y` · `MSSQL_SA_PASSWORD` (ARG, surchargé au runtime via `-e`) |
| Port | `1433` |
| Nom du conteneur | `agirh-sql` (inchangé) |

> **Notion : `FROM … mssql/server:2025-latest`** = on part d'une image officielle prête à l'emploi ; le Dockerfile ne fait que fixer l'environnement. Le mot de passe SA est passé au **runtime** (`docker run -e`), jamais gravé dans l'image.

---

## 3. Démarrage / arrêt (Docker Desktop)

```bash
# Build + démarrage (mot de passe SA passé au runtime, jamais dans l'image)
docker build -t agirh-sql docker/sql
docker run -d --name agirh-sql -p 1433:1433 -e MSSQL_SA_PASSWORD=YourStrong!Passw0rd agirh-sql

# Relancer / arrêter un conteneur existant
docker start agirh-sql
docker stop agirh-sql
docker rm agirh-sql       # supprime le conteneur (pas l'image)
```

**Persistance** : les données SQL vivent dans le volume anonyme du conteneur. Pour un volume nommé durable : `docker run … -v agirh_sql_data:/var/opt/mssql …` (facultatif).

---

## 4. API & frontend : hors Docker

| Composant | Commande | Note |
|---|---|---|
| Backend API | `cd src\Agirh.Api; dotnet run` | migrations + seeds auto, `http://localhost:5000` |
| Frontend | `cd frontend; npm run dev` | BFF + chat, `http://localhost:3000` |

Ollama n'est **pas** conteneurisé : l'API le joint via `AI:Endpoint` (ex. `http://192.168.100.220:11434`). Aucun `host.docker.internal` requis puisque l'API tourne sur l'hôte.

---

## 5. Secrets & configuration

| Secret | Via | Règle |
|---|---|---|
| `MSSQL_SA_PASSWORD` | `docker run -e MSSQL_SA_PASSWORD=…` | jamais dans le Dockerfile ni `appsettings` |
| `JWT_KEY` · `JWT_ISSUER` · `JWT_AUDIENCE` | variables d'environnement (ou secrets du déploiement) | jamais dans `appsettings` committé |
| `ConnectionStrings__Default` | env (`Server=localhost,1433;…`) | mappage `__` = `:` (ASP.NET) |
| `AI__Endpoint` | env | pointe vers le serveur Ollama |

> **Notion** : les fichiers JSON servent au code ; **les variables d'environnement aux secrets**. Une image Docker contient tout ce qui y est copié → ne jamais y mettre de secret.

---

## 6. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0→5 | restructuration docs/notebooklm | ✅ |
| 6 | **Docker révisé : SQL via Dockerfile, API/frontend natifs, compose supprimé** | ✅ |
