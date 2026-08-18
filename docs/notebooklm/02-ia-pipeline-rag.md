# IA — Le pipeline RAG : comment un agent va chercher l'information avant de répondre

*Document autonome, centré sur la partie "retrieval" (récupération) du système d'IA d'AGIRH — la
façon dont le système trouve les bons passages de documentation avant qu'un modèle de langage
n'écrive quoi que ce soit. Le document suivant (03) couvre l'orchestration conversationnelle,
c'est-à-dire ce qui décide *quand* déclencher ce pipeline et comment la réponse finale est
produite.*

## Le problème que RAG résout

Un grand modèle de langage (LLM) génère du texte en prédisant, mot après mot, la suite la plus
probable étant donné tout ce qui précède — y compris ce qu'il a appris pendant son entraînement.
Deux limites structurelles en découlent, et elles ne se corrigent pas en "demandant gentiment" au
modèle d'être honnête :

1. **La connaissance est figée à la date d'entraînement**, et de toute façon générique — un LLM
   entraîné sur du texte public n'a jamais vu les procédures internes réelles d'AGIRH (RH,
   sécurité, matériel), qui n'existent nulle part sur Internet.
2. **L'hallucination** — quand un LLM ne sait pas, il ne dit pas toujours "je ne sais pas" : par
   construction statistique, il peut produire une réponse plausible mais fausse, avec la même
   fluidité et le même ton assuré qu'une réponse correcte. C'est la conséquence directe du fait
   qu'un LLM génère du texte *probable*, pas du texte *vérifié*.

**RAG** (Retrieval-Augmented Generation, popularisé par un papier de recherche Facebook AI en
2020) répond à ces deux limites en changeant la question posée au modèle. Au lieu de demander
"que sais-tu sur X ?" (mémoire interne, non vérifiable, potentiellement obsolète), on lui demande
"voici des extraits réels de documents — réponds *uniquement* à partir de ce qui est écrit ici".
Le modèle ne récite plus sa mémoire, il lit et synthétise un contexte qu'on lui fournit à
l'instant T.

### L'alternative qui n'a pas été retenue : le fine-tuning

Une autre approche pour spécialiser un LLM sur un corpus métier est le **fine-tuning**
(réentraînement partiel du modèle sur des données spécifiques). Elle n'a pas été retenue pour
AGIRH, et comprendre pourquoi éclaire ce que RAG apporte en pratique :

- Le fine-tuning **encode** la connaissance dans les poids du modèle — invisible, non traçable, on
  ne peut pas pointer précisément "cette phrase vient de ce document". RAG au contraire **cite ses
  sources** (`sourced`/`sources` renvoyés par AGIRH à chaque réponse), ce qui est une exigence
  directe du contexte réel du projet (qualité SMSI, traçabilité).
- Une mise à jour de la documentation (un document de procédure qui change) exige de **réentraîner**
  un modèle fine-tuné, une opération coûteuse et lente. Avec RAG, il suffit de réindexer le
  document modifié dans la base vectorielle — une opération de quelques secondes.
- Le fine-tuning ne supprime pas l'hallucination : le modèle peut toujours halluciner sur des
  détails hors de son fine-tuning. RAG, combiné à des garde-fous stricts (voir document 03),
  permet de refuser explicitement de répondre quand rien de pertinent n'est trouvé.

RAG a son propre coût, en échange : une latence supplémentaire à chaque question (le temps de
chercher avant de générer), et une qualité de réponse plafonnée par la qualité du corpus et de la
recherche — un LLM ne peut pas répondre correctement à partir d'un contexte mal trouvé, même s'il
est par ailleurs un bon modèle.

## Les quatre phases du pipeline, en détail

AGIRH implémente un pipeline RAG "complet" à 4 phases, considérées **toutes obligatoires** — c'est
un choix de conception assumé : un pipeline RAG "naïf" (embedding + recherche simple, sans
reranking) est plus rapide à construire mais nettement moins fiable, comme expliqué phase par
phase ci-dessous.

### Phase 1 — Chunking (découpage)

Un document ne peut pas être indexé tel quel : trop long, trop de sujets mélangés. Il faut le
découper en morceaux ("chunks") de taille raisonnable, chacun assez autonome pour être compris
hors contexte.

Deux approches classiques de découpage existent : un découpage **à taille fixe** (ex. tous les 500
mots, sans égard au sens) — simple à implémenter mais brutal, il peut couper une phrase ou une idée
en deux arbitrairement — et un découpage **structurel/sémantique**, qui respecte les frontières
naturelles du document (titres, sections, paragraphes). AGIRH utilise un découpage **structurel**,
basé sur les sections/titres Markdown des documents source, **avec recouvrement** entre chunks
adjacents (une partie du texte de fin d'un chunk réapparaît en début du suivant). Le recouvrement
existe pour éviter qu'une information à cheval sur deux sections ne soit perdue si elle tombe
justement sur la frontière de découpage.

La tokenisation (compter/découper le texte en unités que le modèle comprend) utilise
`Microsoft.ML.Tokenizers`, avec un algorithme WordPiece/SentencePiece — les mêmes familles
d'algorithmes utilisées par les tokenizers de BERT et consorts, qui découpent les mots en
sous-unités (ex. "réindexation" pourrait devenir "ré" + "index" + "ation") pour gérer un
vocabulaire fini malgré une langue à vocabulaire infini.

### Phase 2 — Embedding (vectorisation)

Un **embedding** est une représentation numérique d'un texte sous forme de vecteur (une liste de
nombres, ici 768 dimensions) telle que deux textes proches en *sens* ont des vecteurs proches dans
cet espace à 768 dimensions — même s'ils n'utilisent pas les mêmes mots. C'est ce qui permet une
recherche "sémantique" plutôt qu'une recherche par mots-clés exacts : une question formulée
différemment du document source peut quand même le retrouver.

AGIRH utilise un modèle d'embedding **multilingue** (le corpus est en français, et un modèle
entraîné uniquement en anglais donnerait de mauvais résultats), exécuté via **ONNX Runtime en
.NET pur** — pas d'appel à Ollama pour cette étape. ONNX (Open Neural Network Exchange) est un
format standard et portable pour exécuter des modèles de machine learning déjà entraînés,
indépendamment du framework qui les a produits (PyTorch, TensorFlow...) — ça permet à AGIRH de
faire tourner un modèle d'embedding directement dans le processus .NET, sans dépendance à un
service externe pour cette étape précise, avec un contrôle total sur la latence et la
disponibilité.

**Code réel — l'adaptateur d'embedding** (`src/Agirh.Infrastructure/Rag/OnnxEmbeddingAdapter.cs`) :

```csharp
public sealed class OnnxEmbeddingAdapter : IEmbeddingPort, IDisposable
{
    public int Dimension { get; } = 768;

    private readonly InferenceSession _session;
    private readonly XlmRobertaTokenizer _tokenizer;

    public OnnxEmbeddingAdapter(string onnxModelPath, string sentencePieceModelPath)
    {
        _session = new InferenceSession(onnxModelPath);
        _tokenizer = XlmRobertaTokenizer.LoadFromFile(sentencePieceModelPath);
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var ids = _tokenizer.EncodeToHuggingFaceIds(text);
        // ... tokenisation -> tenseurs input_ids/attention_mask -> InferenceSession.Run -> ...
        // mean-pooling sur les embeddings de tokens (pondéré par attention_mask) + normalisation L2
        // -> un seul vecteur de 768 dimensions représentant la phrase entière.
    }
}
```

> **Règle technique à retenir** : `Dimension` est une propriété **fixe et vérifiée au démarrage**
> (pas une constante magique dispersée) — si un modèle différent était chargé avec une dimension
> différente, `IEmbeddingPort.Dimension` le révélerait immédiatement au lieu de laisser une
> incohérence silencieuse se propager jusqu'à Qdrant, où un mismatch de dimension casserait la
> recherche de façon beaucoup plus difficile à diagnostiquer.

### Phase 3 — Storage (stockage vectoriel et recherche ANN)

Une fois tous les chunks vectorisés, il faut pouvoir, à chaque question, retrouver rapidement les
chunks dont le vecteur est le plus proche du vecteur de la question. Sur un corpus de quelques
documents, une recherche exhaustive (comparer la question à chaque vecteur un par un) serait déjà
viable — mais à l'échelle (des millions de vecteurs dans un cas réel de production), c'est
impraticable. C'est pourquoi les bases vectorielles utilisent une recherche **ANN**
(Approximate Nearest Neighbor) : on accepte de perdre une garantie de trouver *exactement* les
voisins les plus proches, en échange d'une vitesse largement supérieure.

AGIRH utilise **Qdrant**, avec l'algorithme **HNSW** (Hierarchical Navigable Small World) — une
structure de graphe en couches qui permet de naviguer rapidement vers les vecteurs proches sans
tout comparer, un peu comme un index de livre permet de sauter directement au bon chapitre plutôt
que de tout lire. La similarité utilisée est le **cosinus** (l'angle entre deux vecteurs, pas leur
distance brute) — un choix standard pour les embeddings de texte, où c'est la *direction* du
vecteur qui porte le sens, pas sa longueur.

Point intéressant sur le contexte spécifique d'AGIRH : le corpus est volontairement restreint (6
documents rédigés par le porteur du projet lui-même). À cette échelle, Qdrant n'est pas
*strictement* nécessaire techniquement — une recherche exhaustive suffirait largement. La décision
de l'utiliser quand même a été assumée consciemment : la valeur de démontrer une vraie compétence
d'infrastructure IA (déploiement, requêtage d'une base vectorielle réelle) prime sur la simplicité
d'une solution embarquée, dans le contexte d'un projet de fin d'études où cette compétence compte
dans l'évaluation.

### Phase 4 — Reranking (réordonnancement)

C'est la phase la plus souvent omise dans un RAG "de base", et pourtant la plus déterminante pour
la qualité réelle des réponses. Après la recherche vectorielle, on obtient un ensemble de chunks
candidats (top-K, par exemple les 10 ou 20 plus proches) — mais "proche en embedding" ne veut pas
dire "pertinent pour répondre précisément à la question".

Il faut distinguer deux familles de modèles ici :

- Un **bi-encodeur** (utilisé en phase 2) encode la question et chaque document **séparément**,
  indépendamment l'un de l'autre, puis compare leurs vecteurs. C'est rapide (les vecteurs des
  documents sont précalculés une fois pour toutes à l'indexation), mais imprécis, parce que le
  modèle ne "voit" jamais la question et le document ensemble.
- Un **cross-encodeur** (utilisé en phase 4, ici `BAAI/bge-reranker-v2-m3`, en poids ONNX
  directement utilisables sans conversion Python) prend la question **et** un document candidat
  **ensemble**, en une seule passe, et produit un score de pertinence beaucoup plus fin — parce
  que le modèle peut faire attention aux interactions précises entre les mots de la question et
  ceux du document. Le prix à payer : c'est beaucoup plus lent, donc infaisable sur *tout* le
  corpus — d'où l'architecture en deux étages : le bi-encodeur (rapide) réduit le corpus entier à
  quelques candidats, puis le cross-encodeur (lent mais précis) les réordonne finement.

**Code réel — le cross-encodeur de reranking** (`src/Agirh.Infrastructure/Rag/OnnxRerankerAdapter.cs`),
le code exact qui transforme un candidat brut Qdrant en score de pertinence final :

```csharp
public Task<IReadOnlyList<DocumentChunk>> RerankAsync(
    string query,
    IReadOnlyList<DocumentChunk> candidates,
    CancellationToken ct = default)
{
    if (candidates.Count == 0)
        return Task.FromResult<IReadOnlyList<DocumentChunk>>(Array.Empty<DocumentChunk>());

    var results = candidates
        .Select(c => c with { Score = ComputeScore(query, c.Content) })
        .OrderByDescending(c => c.Score)
        .ToList();

    return Task.FromResult<IReadOnlyList<DocumentChunk>>(results);
}

private float ComputeScore(string query, string document)
{
    var ids = _tokenizer.EncodePairToHuggingFaceIds(query, document); // <s> requête </s></s> document </s>
    // ... InferenceSession.Run ...
    var logit = onnxResults.First(r => r.Name == "logits").AsTensor<float>()[0, 0];
    return Sigmoid(logit);
}

private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));
```

Deux détails de syntaxe qui valent la peine d'être compris précisément : `EncodePairToHuggingFaceIds`
construit **une seule séquence de tokens** contenant requête *et* document, séparés par le format
RoBERTa `<s> requête </s></s> document </s>` (deux tokens de séparation `</s></s>` entre les deux
segments, pas un seul — un détail de format qui, s'il est faux, ne provoque aucune erreur mais
dégrade silencieusement la qualité du score). Et le modèle ONNX ne produit qu'un **logit brut** non
borné (`logits[0,0]`) — c'est `Sigmoid` qui le ramène dans l'intervalle [0, 1] pour en faire un
score de pertinence interprétable, exactement comme la dernière couche d'un classifieur binaire.

**Un piège réel rencontré pendant le développement d'AGIRH, à retenir absolument** : un score de
reranking élevé (jusqu'à 0.78 observé sur une échelle où le seuil de pertinence était fixé à 0.01)
ne garantit **pas** que le chunk contient effectivement la réponse à la question — seulement qu'il
est *thématiquement* proche. Un chunk peut parler du même sujet général sans traiter le détail
précis demandé. C'est le texte réellement généré par le modèle générateur (document 03) qui fait
foi, jamais le score de reranking seul. Cette observation est une leçon générale et transférable
sur les systèmes de recherche par similarité : un score de similarité mesure une proximité, pas
une vérité — le confondre avec une preuve de correction est une erreur de conception classique.

## Forces et faiblesses du pipeline RAG, en général et pour AGIRH

**Forces :**
- Les réponses sont **sourcées et vérifiables** — on peut pointer exactement quel document a
  produit quelle réponse, essentiel dans un contexte de conformité qualité.
- **Pas de réentraînement** nécessaire pour intégrer une nouvelle information : réindexer un
  document suffit.
- Combiné à des garde-fous de génération (document 03), permet un mode d'échec "gracieusement
  faux" plutôt qu'une hallucination confiante : dire "je ne sais pas" plutôt qu'inventer.

**Faiblesses :**
- La qualité de la réponse finale est **plafonnée par la qualité de la recherche** — un pipeline
  RAG ne peut pas répondre correctement à partir d'un contexte mal trouvé, quelle que soit la
  qualité du modèle générateur ensuite ("garbage in, garbage out").
- **Latence cumulée** : chunking (déjà fait à l'indexation, donc hors chemin critique), mais
  embedding de la question, recherche Qdrant, et reranking cross-encodeur s'additionnent à chaque
  question posée, avant même que le modèle générateur ne commence à écrire.
- **Le reranking n'est pas une preuve de justesse** (voir piège ci-dessus) — un système de
  vérification supplémentaire (comme celui décrit dans le document 03 : ne jamais faire confiance
  au score seul, toujours passer par la génération et sa propre capacité à refuser) reste
  nécessaire.
- Un corpus mal structuré ou mal découpé (chunking) dégrade tout le reste du pipeline en amont —
  la qualité du chunking a un effet démultiplicateur sur toutes les phases suivantes.
