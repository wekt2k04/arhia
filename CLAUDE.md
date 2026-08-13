# AGIRH — Assistant RH Agentique

Assistant RH conversationnel (.NET 8 hexagonal + Next.js 15). État : **V7** · 122/122 tests · build 0 erreur.

## Règle d'or
Le socle technique (pipeline Actor-Critic, Zero-Trust, RAG, BFF, tests) est **verrouillé**. Tout ajout s'y greffe via le principe Ouvert/Fermé. Aucune refonte de la tuyauterie sans discussion explicite.

## Commandes essentielles
```powershell
dotnet build Agirh.sln -c Release          # build complet (cible : 0 erreur)
dotnet test -c Release                     # suite backend (cible : 122/122)
cd frontend && npm run build               # build frontend (cible : 0 erreur)
cd frontend && npm run dev                 # dev frontend http://localhost:3000
cd src\Agirh.Api && dotnet run             # API http://localhost:5000
```

## Profils de lancement (`launchSettings.json`)
| Profil | Env | Ollama | Modèles |
|---|---|---|---|
| `Agirh_Bureau` | Development | `192.168.100.220:11434` | `phi4-mini:3.8b` · `qwen3.5:9b` · `embeddinggemma` |
| `Agirh_Maison` | local | `localhost:11434` | `phi4-mini:3.8b` · `gemma4:12b` · `embeddinggemma` |

## Architecture
```
Domain (pur) → Core (ports + RbacMatrix + use cases)
             → Infrastructure/Services/ (Profiler, Synthesizer, Checker, Orchestrator, PreFlight, ZeroTrust, WorkerExecutor)
             → Infrastructure/MAF/      (ChecklistFunctions, LeaveFunctions, PayrollFunctions, RagFunctions, EmployeeFunctions, ITSecurityFunctions)
             → Api/                     (Controllers, Dtos, Program.cs, knowledge_base/)
             → frontend/                (Next.js 15, BFF, cookie httpOnly)
```

## Pipeline chat (6 acteurs)
`AgentController(SSE) → AgentOrchestratorService → Profiler(phi4-mini) → PreFlightValidator → ZeroTrustDispatcher(RbacMatrix) → WorkerExecutor(MAF) → Synthesizer → Checker(qwen3.5:9b)`

## Points critiques V7
- **Checker** : `qwen3.5:9b` (reasoning model) — envoyer `think:false` si drift détecté ; `num_predict=64` ; `rawData` borné 500 mots.
- **Profiler** : Règle 7 (mots-clés doc → KnowledgeSearch) + garde C# dans `ProfilerService.cs`.
- **GreetingClassifier** supprimé — salutations via Profiler LLM → GeneralChat.
- **Embedding** : `embeddinggemma:latest` = 768d (garde fail-fast dans `OllamaEmbeddingGenerator.cs:21,57-60`).
- **TOCTOU** : index unique filtré sur `(SourceFile, ChunkIndex)` et `LeaveRequest`.

## Logs runtime
- `src\Agirh.Api\logs\agirh-api.log` — trace plain-text (tronqué au démarrage, heure locale)
- `src\Agirh.Api\logs\agirh-audit.jsonl` — JSONL structuré (tronqué au démarrage, UTC → +1h vs api.log)

## Comptes démo
| Rôle | Email | MDP |
|---|---|---|
| Admin | `admin@agirh.fr` | `admin123` |
| Manager | `marie.martin@agirh.fr` | `manager123` |
| Collaborateur | `jean.dupont@agirh.fr` | `collab123` |

## Agents disponibles (invoquer via Agent tool)
| Agent | Quand l'invoquer |
|---|---|
| `hexagonal-architect` | Design de classe, service, port, DI, Program.cs |
| `secops-guardian` | Endpoint auth, RBAC, injection, secret |
| `ai-rag-specialist` | Pipeline RAG, embeddings, routing MAF, prompts LLM |
| `qa-executioner` | Écriture ou revue de tests, couverture, edge cases |
| `log-sentinel` | Avant/après tout changement endpoint ou config IA — lit les logs runtime |

## Commandes slash disponibles
`/review-arch` · `/audit-security` · `/review-rag` · `/test-command` · `/council`
