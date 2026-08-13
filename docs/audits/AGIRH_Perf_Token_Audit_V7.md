# AGIRH — Audit Performance & Tokens V7

> **Date :** 13/08/2026 · **Portée :** pipeline IA complet — latences, tokens, taux d'échec
> **Période analysée :** 12:24 → 12:37 (agirh-api.log) · profil `Agirh_Bureau` (192.168.100.220:11434)
> **Verdict :** 🔴 **4 anomalies dont 1 CRITIQUE bloquant 17 % des requêtes**

---

## 1. Résumé exécutif

Sur 23 requêtes enregistrées, **4 ont abouti à une réponse fallback** (`NotStreamed`) et 2 ont retourné une base documentaire vide. Le taux de succès réel du pipeline complet est **78 %** — insuffisant pour une démonstration. La cause racine est une **incohérence de configuration** entre `appsettings.json` et `AIOptions.cs` qui désactive silencieusement la boucle de réflexion Actor-Critic.

| Catégorie | Occurrences | Sévérité | État |
|---|---|---|---|
| NotStreamed (Checker rejects) | 4 / 23 | 🔴 CRITIQUE | → corrigé (voir §5) |
| Token waste (Profiler × 3, Synth × 3, Checker × 5) | — | 🟠 ORANGE | → corrigé |
| Timeouts surdimensionnés | — | 🟠 ORANGE | → corrigé |
| Cold start modèles (première requête +6 s) | 1 | 🟡 JAUNE | → non bloquant |
| RAG — 0 résultats (base vide pour onboarding/offboarding) | 2 | 🟡 JAUNE | → contenu manquant |

---

## 2. Cause racine — Boucle de réflexion Actor-Critic silencieusement désactivée

### 2.1 L'incohérence de configuration

```jsonc
// appsettings.json (runtime)       // AIOptions.cs (défaut code)
"MaxReflectionLoops": 1             MaxReflectionLoops = 2;    ← ignoré
```

La valeur `appsettings.json` écrase le défaut. Le runtime tourne avec **MaxReflectionLoops = 1**.

### 2.2 Ce que ça provoque dans le code

```csharp
while (reflectionAttempt <= _maxReflectionLoops)  // 0 <= 1 → ENTRE
{
    reflectionAttempt++;                            // → 1
    finalDraft = await _synthesizer.DraftResponseAsync(...);
    var evaluation = await _checker.EvaluateAsync(...);

    if (evaluation.IsValid) break;

    feedback = evaluation.ActionableFeedback;       // capturé...
    if (reflectionAttempt >= _maxReflectionLoops)   // 1 >= 1 → BREAK immédiat
        break;                                      // ...mais jamais utilisé
}
// résultat : 1 seul essai, feedback généré mais JAMAIS réinjecté au Synthesizer
```

**Conséquence** : le Checker rejette (à juste titre) les hallucinations du Synthesizer, mais la deuxième tentative avec feedback correctif ne se produit jamais. Le mécanisme Actor-Critic est fonctionnellement **mort**.

### 2.3 Preuves dans les logs (3 cas analysés)

| Audit row | Intention | Rejet Checker | Feedback (tronqué) |
|---|---|---|---|
| 5 | LeaveBalance (Admin) | `is_valid:false` | "mention de 'l'administrateur a un solde de 0 jour' — donnée inventée" |
| 14 | GeneralInquiry (Collab) | `is_valid:false` | "AGIRH = CIRS à l'HU McGill — affirmation non appuyée par les données" |
| 16 | LeaveBalance (Collab) | `is_valid:false` | "jean.dupont@agirh.fr présenté comme adresse de contact — erreur factuelle" |

Dans les 3 cas, le Checker est **correct** : le Synthesizer (phi4-mini:3.8b) invente des détails. Avec MaxReflectionLoops=2, la deuxième synthèse recevrait ce feedback et corrigerait l'hallucination.

---

## 3. Gaspillage de tokens (audit par modèle)

### 3.1 Profiler — phi4-mini:3.8b

| Paramètre | Valeur actuelle | Usage réel (logs) | Surplus |
|---|---|---|---|
| `num_predict` | **512** | ~80 tokens (JSON intent) | **× 6.4** |
| Valeur recommandée | **150** | — | — |

Le Profiler génère un JSON court (`intention`, `confidence`, `entities`, `mainIdea`) — jamais plus de 100 tokens. `num_predict=512` force Ollama à réserver 512 tokens de budget KV en plus, ralentissant chaque inférence.

### 3.2 Synthesizer — phi4-mini:3.8b

| Paramètre | Valeur actuelle | Usage réel (logs) | Surplus |
|---|---|---|---|
| `num_predict` | **160** | 7 à 50 tokens | **× 3 à 22** |
| Valeur recommandée | **80** | — | — |

Réponses de 2 phrases max. 160 tokens est 2-20x le besoin réel.

### 3.3 Checker — qwen3.5:9b

| Paramètre | Valeur actuelle | Usage réel (logs) | Surplus |
|---|---|---|---|
| `num_predict` | **256** | 14 à 50 tokens | **× 5 à 18** |
| Valeur recommandée | **128** | — | — |

Verdict JSON court : `{"is_valid": true/false, "actionable_feedback": "..."}` — 30-50 tokens dans les cas nominaux.

---

## 4. Timeouts surdimensionnés

| Client HTTP | Timeout actuel | Latence max observée | Timeout recommandé |
|---|---|---|---|
| Profiler (phi4-mini) | **300 s** | 4 794 ms (cold) · 800 ms (warm) | **45 s** |
| Synthesizer (phi4-mini) | **120 s** | ~800 ms | **30 s** |
| Checker (qwen3.5:9b) | **120 s** | 6 234 ms (cold) · 1 600 ms (warm) | **45 s** |
| Embedding (embeddinggemma) | **180 s** | 2 244 ms (cold) · 190 ms (warm) | **15 s** |

Un timeout à 300 s masque les blocages réseau vers Ollama. Avec Polly (3 retries, backoff 200→400→800 ms), les valeurs recommandées couvrent 3 × cold_start + délais.

---

## 5. Métriques observées (référence avant correctifs)

### 5.1 Distribution des outcomes (23 requêtes)

| Outcome | Count | % |
|---|---|---|
| Success | 14 | 61 % |
| NotStreamed (Checker reject) | 4 | 17 % |
| WorkerError (RAG vide) | 2 | 9 % |
| ValidationClarification | 2 | 9 % |
| Unknown | 1 | 4 % |

### 5.2 Latences par étape (PIPELINE_TIMINGS — succès uniquement)

| Étape | Min | Median | Max | Bottleneck ? |
|---|---|---|---|---|
| Profiler | 539 ms | 669 ms | 4 794 ms | Cold start seulement |
| Validator | 0 ms | 0 ms | 0 ms | — |
| Dispatcher | 0 ms | 0 ms | 5 ms | — |
| Worker (tool) | 0 ms | 13 ms | 35 ms | — |
| **Reflection (Synth+Check)** | **920 ms** | **1 400 ms** | **6 664 ms** | **✗ Dominant (40-70 %)** |

La phase Reflection est le seul vrai goulot : 1-6 s selon l'état du KV cache de qwen3.5:9b.

### 5.3 Tokens consommés

| Pipeline | tokenCount médian | Fallback (NotStreamed) |
|---|---|---|
| Success | 30 à 50 tokens | — |
| NotStreamed | **1 token** | Synthesizer + Checker exécutés pour rien |

Chaque `NotStreamed` consomme ~2 s de Synthesizer + ~1.5 s de Checker sans produire de réponse utile — un doublement de coût pour zéro valeur.

---

## 6. Cold Start & Doubles Profiler Calls

### 6.1 Cold start (première requête de session)

- Profiler (phi4-mini) : **4 794 ms** → normal: 600-800 ms
- Checker (qwen3.5:9b) : **6 234 ms** → normal: 500-1 600 ms
- Total première requête : **~11 s** vs ~2 s en régime établi

**Cause** : Ollama charge les poids des modèles en VRAM à la première inférence. Aucun mécanisme de pré-chauffe côté API.

### 6.2 Doubles appels Profiler (MaxExtractionRetries = 2)

Observé sur 4 séquences : 2 appels Profiler consécutifs < 1 s d'intervalle. Comportement attendu (retry quand ValidationResult échoue), coût additionnel ~600 ms par retry.

---

## 7. RAG — 0 résultats (base documentaire insuffisante)

| Query | Embedding | Chunks retournés | Cause |
|---|---|---|---|
| "Définition de RH et onboarding" | 2 244 ms (cold) | **0** | Contenu absent dans knowledge_base/ |
| "Onboarding et Offboarding" | 190 ms (warm) | **0** | Contenu absent dans knowledge_base/ |

Le pipeline RAG fonctionne correctement (embedding OK, VECTOR_DISTANCE OK, seuil 0.60). La base documentaire ne couvre pas encore l'onboarding/offboarding — c'est un manque de contenu, pas un bug.

---

## 8. Correctifs appliqués

| # | Fichier | Avant | Après | Motif |
|---|---|---|---|---|
| C1 🔴 | `appsettings.json` | `MaxReflectionLoops: 1` | `MaxReflectionLoops: 2` | Active la boucle Actor-Critic |
| C2 🟠 | `ProfilerService.cs` | `num_predict = 512` | `num_predict = 150` | Réduit surplus × 6 |
| C3 🟠 | `SynthesizerAgent.cs` | `num_predict = 160` | `num_predict = 80` | Réduit surplus × 3 |
| C4 🟠 | `CheckerAgent.cs` | `num_predict = 256` | `num_predict = 128` | Réduit surplus × 5 |
| C5 🟠 | `appsettings.json` | `ProfilerTimeout: 300` | `ProfilerTimeout: 45` | Borne le timeout phi4-mini |
| C6 🟠 | `appsettings.json` | `SynthesizerTimeoutSeconds: 120` | `SynthesizerTimeoutSeconds: 30` | Borne le timeout phi4-mini |
| C7 🟠 | `appsettings.json` | `CheckerTimeoutSeconds: 120` | `CheckerTimeoutSeconds: 45` | Borne le timeout qwen3.5:9b |
| C8 🟠 | `appsettings.json` | `EmbeddingTimeoutSeconds: 180` | `EmbeddingTimeoutSeconds: 15` | Borne le timeout embeddinggemma |

---

## 9. Recommandations non appliquées

| Priorité | Recommandation | Motif |
|---|---|---|
| 🟡 | Pré-chauffe des modèles au démarrage API (`IHostedService` → POST /api/generate à chaque modèle) | Élimine le cold start de 11 s sur la première requête |
| 🟡 | Alimenter la knowledge_base avec du contenu onboarding/offboarding | Rendre KnowledgeSearch utile sur ces sujets |
| 🟡 | Augmenter la limite de truncation du log Checker (actuellement 500 chars) | Feedback souvent tronqué dans les logs de rejet |
