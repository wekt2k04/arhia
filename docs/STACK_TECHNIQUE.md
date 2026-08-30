# Stack technique — arhia V8

*Document de cadrage issu d'une série de questions/réponses avec le porteur du projet, suite à `LOGIQUE_METIER.md`. Sert de référence pour `ARCHITECTURE.md` (hexagonal, arborescence, diagrammes) et pour tout agent custom (`.claude/agents/`). Document vivant — mis à jour à chaque décision technique qui change.*

## 0. Principe directeur

Les choix de framework (backend/frontend) sont conservés de V7 — expertise déjà acquise, pas de valeur ajoutée à réapprendre un stack pour un projet de stage à périmètre resserré. En revanche, tout ce qui touche à l'IA (pipeline RAG, orchestration conversationnelle) et à l'infrastructure de données a été **repensé de zéro**, sans reprendre les décisions V7 par défaut — voir `HISTORIQUE.md` pour la trace des raisons de la remise à zéro.

## 1. Backend

- **.NET 8** (LTS) en pratique — seul SDK installé sur la machine de développement au moment de démarrer l'implémentation (vérifié via `dotnet --list-sdks`, milestone 3). La décision d'origine (`HISTORIQUE.md`) visait .NET 10 (LTS plus récent) ; à réévaluer si le SDK .NET 10 est installé plus tard — changement de TFM à faible coût, pas structurant.
- ASP.NET Core, EF Core.
- Architecture hexagonale (Domain/Core/Infrastructure/Api) — détail complet dans `ARCHITECTURE.md`.
- **Authentification** : JWT + ASP.NET Identity. Keycloak (envisagé dans l'historique V7→V8) est abandonné — hors de proportion pour l'échelle du prototype (3 rôles, ~5 RH + 2 Admin/Qualité + collaborateurs, LOGIQUE_METIER.md §1). Auto-inscription → rôle Employee par défaut ; élévation de rôle réservée à Admin/Qualité.
- **Temps réel** : SSE (Server-Sent Events), pour le flux de chat et pour la barre de notifications par pôle/domaine (LOGIQUE_METIER.md §9). Un seul mécanisme de transport pour les deux usages.

## 2. Frontend

- **Next.js** (BFF pattern, cookie httpOnly — le frontend ne détient jamais le JWT en clair côté client).
- Interface **agent-first** (LOGIQUE_METIER.md §9) : page de garde publique, puis après connexion chat + barre de notifications comme point d'entrée principal ; pas de dashboard structuré séparé dans la première itération. **Mise à jour (2026-08-29/30)** : un layout authentifié partagé a depuis été ajouté (tableau de bord par rôle, pages Dossiers et Collaborateurs), sur constat direct que le chat seul ne suffisait pas à exposer les fonctionnalités backend déjà codées — le chat reste un point d'entrée, plus le seul.
- TailwindCSS, react-markdown pour le rendu des réponses de l'agent.
- **shadcn/ui** (composants copiés dans `frontend/components/ui/`, pas une dépendance npm classique) bâti sur **Radix UI** pour les primitives accessibles, décidé le 2026-08-16 pour l'amélioration UX/UI (branding, chat, accessibilité). Style "new-york", palette indigo via CSS variables (`app/globals.css`).

## 3. Données

Deux services de données, tous deux en Docker :

- **SQL Server** — entités relationnelles métier : Employee, Department, WorkflowTemplate/Instance, Item, comptes/rôles. Réutilise l'infra et l'expertise EF Core déjà en place.
- **Qdrant** — index vectoriel du pipeline RAG (recherche ANN via HNSW, similarité cosinus). Décision confirmée malgré un corpus volontairement restreint (documents rédigés par le porteur du projet) : la valorisation de la compétence infra IA dans le rendu du PFA prime sur la simplicité d'une solution embarquée.

## 4. Pipeline RAG — 4 phases, toutes obligatoires

Repensé intégralement (pas de reprise de l'embedding `embeddinggemma` via Ollama utilisé en V7) :

| Phase | Choix | Détail |
|---|---|---|
| **1. Chunking** | Découpage **structurel** (sections/headings Markdown) **avec recouvrement** entre chunks adjacents | Tokenisation via `Microsoft.ML.Tokenizers` (WordPiece + SentencePiece dans le même paquet officiel Microsoft) |
| **2. Embedding** | **ONNX Runtime en .NET pur**, modèle multilingue (ex. `paraphrase-multilingual-mpnet-base-v2`, 768d) | Corpus en français → modèle multilingue nécessaire. Pas de dépendance à Ollama pour cette étape — contrôle total du pipeline en .NET |
| **3. Storage** | **Qdrant**, ANN/HNSW, similarité cosinus | Cf. §3 |
| **4. Reranking** | Cross-encoder **ONNX** (`BAAI/bge-reranker-v2-m3`) | Poids ONNX publiés directement, pas de conversion Python nécessaire. Phase **obligatoire**, pas une amélioration optionnelle — c'est ce qui distingue un vrai pipeline RAG d'un RAG naïf |

## 5. Orchestration conversationnelle

Distincte du pipeline RAG ci-dessus — c'est la partie qui produit la réponse conversationnelle elle-même :

```
Router (Ollama, petit modèle)  → intention : documentaire | statut de dossier | hors-périmètre
Generator (Ollama, modèle plus capable) → réponse finale, sourcée si RAG utilisé
```

- **Ollama local, `phi4-mini:3.8b` pour le routeur ET le générateur** — écart mesuré par rapport à l'intention initiale (2 modèles distincts) : `gemma4:12b` (candidat "modèle plus capable") a été testé en réel sur cette machine et met **plus de 2 minutes sans produire de réponse**, même pour une question courte — pas de GPU adapté ici, CPU-only inefficace pour un modèle 12B. `phi4-mini:3.8b` répond en quelques secondes et reste la seule option praticable sur cette infra pour les deux rôles. Réutilise l'infrastructure Ollama déjà opérationnelle et déjà peuplée (`phi4-mini:3.8b`, `gemma4:12b`, `embeddinggemma`, `all-minilm` présents localement).
- Le prompt du routeur nécessite des règles explicites + exemples few-shot pour classifier correctement (testé empiriquement : un prompt minimal classe à tort une question générale — "qui signe la fiche de décharge ?" — comme une question de statut personnel). La sortie du routeur n'est jamais utilisée telle quelle : elle est validée contre un enum fermé, tout ce qui ne matche pas exactement `DOCUMENTAIRE`/`STATUT_DOSSIER` (y compris une sortie vide, un timeout, ou un mot inventé par le modèle) retombe sur `HORS_PERIMETRE` par défaut — fail-safe, pas fail-open.
- L'agent reste **informatif uniquement** (LOGIQUE_METIER.md §9) : le Router/Generator ne déclenchent jamais d'action destructrice — une question de statut de dossier passe par un port de lecture seule vers `WorkflowInstance`, jamais par une écriture.
- Si du matériel avec GPU devient disponible, `gemma4:12b` (ou un modèle intermédiaire) redevient un candidat raisonnable pour le générateur seul — pas pour le routeur, où la latence doit rester courte.

## 6. Observabilité

Deux flux de logs **séparés** :
- **Log technique** (debug/erreurs) — priorité : faciliter le débogage en développement.
- **Audit trail** — trace métier : qui a coché quel item, qui a validé/rejeté un template (circuit Rédacteur/Vérificateur/Approbateur, LOGIQUE_METIER.md §6), création/clôture/archivage d'un `WorkflowInstance`. Exigé par la nature "conformité qualité SMSI" du processus réel.

Schéma exact (format de ligne, champs) à fixer à l'implémentation — voir `.claude/agents/log-sentinel.md`, qui documente cette approche comme contrat cible.

## 7. Déploiement

**Docker Compose 100% local** pour la démo/soutenance du PFA — API, frontend, SQL Server, Qdrant, Ollama orchestrés sur la machine du porteur de projet. Pas de dépendance à un hébergement externe.

## 8. Tests

Même rigueur que V7 : **xUnit + Moq + FluentAssertions**, `qa-executioner` mobilisé systématiquement à chaque milestone (voir `CHECKLIST.md`). Cible : suite verte (N/N) à chaque étape, pas seulement en fin de projet.

## 9. Récapitulatif

| Composant | Choix |
|---|---|
| Backend | .NET 8 (SDK installé ; .NET 10 visé initialement, à réévaluer), ASP.NET Core, EF Core |
| Frontend | Next.js (BFF, cookie httpOnly), TailwindCSS, shadcn/ui (Radix), react-markdown |
| Auth | JWT + ASP.NET Identity |
| Temps réel | SSE |
| Base relationnelle | SQL Server (Docker) |
| Base vectorielle | Qdrant (Docker, ANN/HNSW/cosine) |
| Chunking | Structurel + recouvrement, `Microsoft.ML.Tokenizers` |
| Embedding | ONNX Runtime .NET, multilingue 768d |
| Reranking | ONNX cross-encoder `BAAI/bge-reranker-v2-m3` (obligatoire) |
| LLM Router + Generator | Ollama local, 2 modèles |
| Logs | Technique (debug) + audit trail, séparés |
| Déploiement démo | Docker Compose local |
| Tests | xUnit + Moq + FluentAssertions |

## 10. Ouvert / en attente

- Noms/tailles précis des modèles Ollama pour Router et Generator — à fixer selon ce qui tourne correctement sur l'infra disponible.
- Schéma exact des deux flux de logs (champs, format de ligne).
- Version précise de Next.js/EF Core à épingler à l'implémentation.
