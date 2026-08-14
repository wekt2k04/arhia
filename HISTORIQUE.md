# Historique du projet AGIRH

## V7 — ce qui a été construit

Assistant RH conversationnel : .NET 8 (architecture hexagonale à 4 projets : Domain/Core/Infrastructure/Api) + Next.js 15 (BFF, cookie httpOnly), SQL Server 2025, LLM via Ollama (`phi4-mini:3.8b`, `qwen3.5:9b`, `embeddinggemma`). 122/122 tests, build 0 erreur.

Pipeline agentique à 6 étapes : `Profiler → PreFlightValidator → ZeroTrustDispatcher → WorkerExecutor → Synthesizer → Checker` (pattern Actor-Critic / Evaluator-Optimizer entre Synthesizer et Checker). RAG : recherche vectorielle native SQL Server (`vector(768)`), embeddings `embeddinggemma`, chunking par découpage naïf en mots (512 mots / 128 de recouvrement). RBAC par outil (`RbacMatrix`, rôles Admin/Manager/Collaborator) + garde Zero-Trust déterministe (`ZeroTrustDispatcher`). Domaine métier livré : Onboarding (checklist), Offboarding (révocation accès IT, alerte manager), congés/CET, paie (avance sur salaire).

## Pourquoi la remise à zéro

**RAG mal fondé** — aucun reranking, pas d'index vectoriel dédié (SQL Server natif), chunking qui ignore la structure des documents.

**Pipeline trop long, et cassé silencieusement** — 6 étapes séquentielles pour une seule boucle de correction réelle. Bug de configuration confirmé ligne par ligne dans `AgentOrchestratorService.cs` (loop `reflectionAttempt`/`_maxReflectionLoops`, ~ligne 210-220) : avec `MaxReflectionLoops=1` (valeur en prod dans `appsettings.json`), le `break` sort de la boucle juste après avoir calculé le feedback du Checker — qui n'est donc **jamais** réellement réinjecté dans une 2ᵉ passe du Synthesizer. L'auto-correction Actor-Critic n'a jamais fonctionné en configuration réelle.

**Dérive de périmètre métier** — `SUJET_STAGE.md` (le vrai sujet donné par l'entreprise) ne demande qu'un agent Onboarding/Offboarding. V7 a construit en plus congés/CET/paie/avance sur salaire (hors sujet) tout en sous-livrant l'onboarding/offboarding réel : pas de suivi documentaire (RIB/CIN/diplômes), pas de gestion de matériel IT, pas d'entretien de sortie, pas de personnalisation par poste/entité/pays/contrat, alertes limitées à un seul manager.

## Décisions actées pour la suite (V8)

| Sujet | Décision |
|---|---|
| Périmètre métier | Onboarding/Offboarding uniquement — congés/CET/paie/avance supprimés |
| Authentification | Keycloak, flux Authorization Code + PKCE (remplace JWT+BCrypt maison) |
| Base vectorielle | Qdrant (remplace `vector(768)` SQL Server natif) |
| Reranking | Cross-encoder HuggingFace `BAAI/bge-reranker-v2-m3`, en .NET pur via ONNX Runtime (poids ONNX déjà publiés, pas de conversion Python nécessaire) |
| Embeddings | Multilingue `sentence-transformers/paraphrase-multilingual-mpnet-base-v2` (768 dim), ONNX Runtime |
| Tokenization | `Microsoft.ML.Tokenizers` (officiel Microsoft, couvre WordPiece ET SentencePiece dans le même paquet — les deux modèles ci-dessus sont XLM-RoBERTa/SentencePiece) |
| Pipeline agentique | 3 acteurs max : Router/Guard (intention + validation + RBAC), Executor (outils + RAG), Responder (génération + critique — garde 2 appels LLM indépendants pour préserver l'auto-correction) |
| Base vectorielle infra | `Qdrant.Client` (officiel, gRPC, confirmé sur NuGet) |
| .NET | net10.0 (LTS courant) |

## Notes en vrac (reprises de IMPORTANT.txt)

- Supprimer la logique de salutation spéciale codée en dur, passer plutôt par un modèle.
- Mieux résumer les étapes (steps) présentées à l'utilisateur.
- Référence : https://www.keycloak.org/
- Idée à explorer : un "LLM visualizer".
- Idée à explorer : un transformer dédié à l'extraction de données (en remplacement/complément du Profiler actuel ?).

## État du code V7

Le code complet de V7 (avant remise à zéro) reste récupérable via le tag git **`v7-archive`** — ex. `git show v7-archive:src/Agirh.Infrastructure/Services/AgentOrchestratorService.cs` pour retrouver un fichier précis, ou `git checkout v7-archive -- <chemin>` pour le restaurer.

*Document créé le 2026-08-14 lors de la remise à zéro complète du projet.*
