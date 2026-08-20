# IA — Orchestration conversationnelle : Router, Generator, et les garde-fous d'un agent fiable

*Document autonome. Le document précédent (02) explique comment AGIRH retrouve l'information
pertinente (le pipeline RAG). Celui-ci explique ce qui décide *quand* utiliser cette information,
comment la réponse finale est écrite, et surtout comment le système se protège contre les
faiblesses connues des LLM — hallucination, dérive, actions non désirées.*

## Ce que "orchestration" veut dire ici

Un agent conversationnel de production ne se résume presque jamais à "envoyer la question à un
LLM et afficher sa réponse". Une question posée dans le chat d'AGIRH peut relever de natures très
différentes : une question sur une politique interne (documentaire, traitée par RAG), une demande
de statut d'un dossier personnel (une lecture de données structurées, pas de recherche
documentaire), ou une question totalement hors sujet. Traiter les trois cas de la même façon
produirait soit des réponses hors sujet, soit des recherches inutiles, soit pire, des réponses
inventées.

**L'orchestration**, c'est la couche qui décide du chemin à emprunter avant de produire une
réponse. AGIRH sépare cette décision (le **Router**) de la production de la réponse elle-même (le
**Generator**) — deux rôles distincts, potentiellement deux modèles distincts, appelés en
séquence :

```
Router (Ollama)    → classifie l'intention : DOCUMENTAIRE | STATUT_DOSSIER | HORS_PERIMETRE
Generator (Ollama)  → écrit la réponse finale, avec le contexte approprié selon le cas
```

Ce pattern **routeur + générateur** (parfois appelé "intent classification + response
generation") est courant dans les systèmes conversationnels de production, y compris au-delà des
LLM : c'est le même principe qu'un standard téléphonique qui identifie d'abord la nature de
l'appel avant de le rediriger vers le bon interlocuteur. Il permet à chaque composant d'être
spécialisé et plus simple à raisonner isolément, plutôt qu'un seul modèle "généraliste" qui devrait
tout gérer d'un coup.

## Pourquoi un LLM local (Ollama) plutôt qu'une API cloud

AGIRH tourne entièrement sur **Ollama**, un moteur d'inférence LLM local (le modèle tourne sur la
machine, pas sur un serveur distant type OpenAI ou Anthropic). Ce choix a des implications
importantes, dans les deux sens :

**Ce qu'on y gagne :**
- **Souveraineté des données** — les questions posées (potentiellement liées à des dossiers RH
  personnels) ne quittent jamais l'infrastructure locale/de l'entreprise. Pour un système RH, où
  les données sont sensibles par nature, c'est un argument structurant, pas accessoire.
- **Coût prévisible** — pas de facturation à l'appel API, important pour un prototype qui va
  tourner beaucoup de fois pendant le développement et les tests.
- **Disponibilité hors ligne** — pas de dépendance à un service tiers pour fonctionner.

**Ce qu'on y perd :**
- **Capacité du modèle** — un modèle local exécutable sur du matériel de bureau (sans GPU dédié
  puissant) est très en retrait par rapport aux modèles de pointe accessibles par API (GPT, Claude,
  Gemini). C'est un compromis assumé, pas une méconnaissance de la différence de qualité.
- **Latence et débit dépendants du matériel local** — pas d'infrastructure massive derrière, donc
  les temps de réponse dépendent directement de la machine qui exécute Ollama.

## Le compromis modèle : taille, matériel, et un écart de conception assumé

L'intention initiale du projet prévoyait deux modèles distincts : un petit modèle rapide pour le
Router (classification simple, doit répondre vite), et un modèle plus capable pour le Generator
(écriture de réponse, tâche plus exigeante). Dans les faits, les deux rôles utilisent le **même**
modèle, `phi4-mini:3.8b` (3,8 milliards de paramètres) — et comprendre pourquoi est instructif sur
les contraintes réelles du déploiement de LLM sur du matériel limité.

Un modèle candidat plus capable, `gemma4:12b` (12 milliards de paramètres, donc environ 3 fois
plus de paramètres que `phi4-mini`), a été testé en conditions réelles sur la machine de
développement — sans GPU adapté pour l'inférence. Résultat mesuré : plus de deux minutes sans
produire de réponse, même pour une question courte. En inférence LLM, la taille du modèle et le
temps de calcul par token généré sont directement liés ; sans accélération matérielle (GPU), un
modèle 3× plus gros peut devenir totalement impraticable pour un usage interactif, pas seulement
"un peu plus lent". C'est une illustration concrète d'un principe général en IA appliquée : la
capacité théorique d'un modèle ne vaut rien si l'infrastructure ne peut pas l'exécuter dans un
temps acceptable pour l'usage visé. `phi4-mini:3.8b`, qui répond en quelques secondes, reste donc
la seule option praticable ici pour les deux rôles — un compromis documenté et assumé plutôt que
caché, avec une piste de reprise explicite si du matériel avec GPU devient disponible (le
générateur redeviendrait alors un candidat raisonnable pour un modèle plus gros ; le routeur, où
la latence doit rester courte, resterait sur un petit modèle).

## Prompt engineering : ce qui marche, ce qui ne marche pas

Le Router a besoin, pour bien classifier, d'un prompt contenant des **règles explicites** et des
**exemples few-shot** (quelques exemples de questions déjà classées, donnés dans le prompt pour
que le modèle "voie" le pattern attendu). Un prompt minimal, sans ces exemples, classe à tort une
question générale ("qui signe la fiche de décharge ?") comme une question de statut personnel —
alors qu'elle relève en réalité d'une question documentaire sur une procédure.

**Code réel — le prompt système complet du Router** (`src/Agirh.Infrastructure/Llm/OllamaRouterAdapter.cs`),
tel qu'il est réellement envoyé au modèle à chaque question :

```
Tu es un classifieur d'intention pour un assistant RH interne. Classe la question dans EXACTEMENT
une categorie parmi les trois suivantes. Reponds UNIQUEMENT par un de ces 3 mots exacts, en
majuscules, rien d'autre : DOCUMENTAIRE, STATUT_DOSSIER, HORS_PERIMETRE.

Regle cle : si la question ne contient PAS "mon", "ma", "je", "j'ai", ou "moi", classe-la TOUJOURS
en DOCUMENTAIRE (jamais STATUT_DOSSIER), meme si elle parle de dossier, fiche ou signature en
general.

DOCUMENTAIRE : question generale sur une politique, regle, procedure ou charte de l'entreprise,
applicable a tout le monde.
"Quelle est la politique de mot de passe ?" -> DOCUMENTAIRE
"Qui signe la fiche de decharge ?" -> DOCUMENTAIRE (question generale sur QUI signe, pas sur MON dossier)

STATUT_DOSSIER : question sur l'avancement du dossier PERSONNEL de l'utilisateur, contient
obligatoirement mon/ma/je/j'ai/moi.
"Ou en est mon onboarding ?" -> STATUT_DOSSIER

HORS_PERIMETRE : toute autre question sans lien avec les politiques de l'entreprise ou un dossier
onboarding/offboarding.
```

Puis, côté code, la sortie brute du modèle est parsée ainsi (extrait réel) :

```csharp
var normalized = (rawResponse ?? string.Empty).Trim().ToUpperInvariant();

if (normalized.Contains("STATUT_DOSSIER"))
    return ConversationIntent.CaseStatus;

if (normalized.Contains("DOCUMENTAIRE"))
    return ConversationIntent.DocumentaryQuestion;

// tout le reste (y compris une sortie vide, un timeout, un mot halluciné) -> OutOfScope
```

> **Règle métier à retenir** : la "règle clé" du prompt (mon/ma/je/j'ai/moi comme seul signal
> autorisé pour `STATUT_DOSSIER`) est une **heuristique lexicale explicite écrite en langage
> naturel dans le prompt**, pas une règle codée en C#. C'est précisément la limite documentée
> plus bas : un petit modèle peut échouer à appliquer correctement une règle qui lui est pourtant
> énoncée noir sur blanc.

Une observation empirique importante, obtenue en testant directement sur le projet : **doubler le
nombre d'exemples few-shot dans le prompt du Router n'a eu aucun effet mesurable** sur le taux de
mauvaise classification (~27% mesuré sur un jeu de 48 questions de test, voir plus bas). Cette
tentative a été explicitement testée, mesurée sur des cas réels, puis abandonnée et le prompt
restauré à sa version d'origine. La leçon à en tirer dépasse ce projet précis : la capacité d'un
petit modèle (3,8 milliards de paramètres) à suivre des règles explicites complexes semble
plafonner — ajouter plus d'exemples dans le contexte ne compense pas une limite de capacité de
raisonnement du modèle lui-même. C'est une distinction importante en IA appliquée entre deux leviers
différents : la **taille du contexte fourni** (ce qu'on peut mettre dans le prompt) et la
**capacité de raisonnement du modèle** (ce qu'il peut réellement en tirer) — le premier ne
compense pas indéfiniment les limites du second.

## Les garde-fous : ne jamais faire une confiance aveugle à la sortie d'un LLM

C'est le cœur de ce qui rend un système à base de LLM utilisable en production plutôt que comme
simple démonstration : traiter la sortie du modèle comme une donnée **non fiable par défaut**, à
valider avant de l'utiliser pour une décision — jamais comme une vérité à exécuter directement.

### Fail-safe, pas fail-open

La sortie brute du Router (du texte libre généré par le modèle) n'est **jamais** utilisée telle
quelle. Elle est validée contre un **enum fermé** à exactement trois valeurs possibles
(`DOCUMENTAIRE`, `STATUT_DOSSIER`, `HORS_PERIMETRE`). Tout ce qui ne matche pas exactement l'une
de ces trois valeurs — une sortie vide, un timeout du modèle, un mot halluciné qui ressemble à une
catégorie sans en être une — retombe **par défaut** sur `HORS_PERIMETRE`.

C'est la différence entre un système **fail-safe** (en cas de doute ou d'échec, refuser/retomber
sur l'option la plus restrictive) et un système **fail-open** (en cas de doute, laisser passer par
défaut). En sécurité comme en fiabilité logicielle, fail-safe est presque toujours le bon choix
par défaut quand l'erreur a un coût : ici, un flou ou une panne du modèle ne peut jamais
accidentellement déclencher un comportement plus permissif que prévu.

### Anti-hallucination encodé dans le code, pas seulement dans le prompt

Une distinction essentielle et souvent mal comprise : demander au modèle, dans son prompt, de "ne
pas halluciner" ou de "dire je ne sais pas si tu ne sais pas" est une instruction **best-effort**
— le modèle peut l'ignorer, l'oublier sur une question ambiguë, ou se tromper sur ce qu'il "sait"
réellement. AGIRH ajoute une protection **structurelle**, en dehors du contrôle du modèle : si la
recherche RAG (document 02) ne retourne **aucun** chunk pertinent (0 candidat, ou tous sous le
seuil de pertinence), le **Generator n'est même pas appelé**. Le système répond directement "je
n'ai pas trouvé cette information", sans jamais donner au modèle l'opportunité d'halluciner une
réponse à partir de rien. C'est un garde-fou en code, vérifiable et testé (couvert par des tests
unitaires dédiés), pas une simple consigne dans un prompt que le modèle pourrait ne pas suivre.

**Code réel — le garde-fou complet** (`src/Agirh.Core/UseCases/AnswerConversationUseCase.cs`),
retrieval → reranking → filtrage par seuil → décision d'appeler ou non le Generator :

```csharp
private const float MinimumRelevanceThreshold = 0.01f;

private async Task<DocumentaryPreparation> PrepareDocumentaryContextAsync(
    string question, CancellationToken ct)
{
    var queryVector = await _embedding.GenerateEmbeddingAsync(question, ct);
    var candidates = await _vectorSearch.SearchAsync(queryVector, TopKSearch, ct);

    if (candidates.Count == 0)
        return new DocumentaryPreparation(false, null, Array.Empty<DocumentChunk>());

    var reranked = await _reranker.RerankAsync(question, candidates, ct);
    var best = reranked
        .Where(c => c.Score >= MinimumRelevanceThreshold)
        .Take(TopKAfterReranking)
        .ToList();

    if (best.Count == 0)
        return new DocumentaryPreparation(false, null, Array.Empty<DocumentChunk>());

    // ... construction du system prompt avec le contexte trouvé ...
    return new DocumentaryPreparation(true, systemPrompt, best);
}
```

Le champ `Found` (premier élément du tuple `DocumentaryPreparation`) est vérifié par l'appelant
**avant** toute tentative d'appel au Generator :

```csharp
var preparation = await PrepareDocumentaryContextAsync(question, ct);
if (!preparation.Found)
    return NotFoundResponse(); // le Generator n'est jamais invoqué dans cette branche
```

> **Règle métier à retenir, la plus importante du document** : il existe **deux** portes de sortie
> anticipée avant le Generator (`candidates.Count == 0` juste après Qdrant, et `best.Count == 0`
> après filtrage par le reranker) — pas une seule. Même si Qdrant retourne des résultats, si aucun
> ne passe le seuil de pertinence post-reranking, le Generator reste non appelé. C'est cette
> double porte, pas une simple instruction de prompt, qui rend le mode de défaillance du système
> "gracieusement faux" plutôt que "confiant et faux".

### Un agent strictement informatif, jamais un agent d'action

Même pour une question de statut de dossier (pas de recherche RAG dans ce cas, lecture directe
d'un `WorkflowInstance`), le chemin de code passe exclusivement par un port de **lecture seule**.
Le Router/Generator ne déclenchent **jamais** d'écriture, de modification, ou d'action
destructrice sur le système — quelle que soit la formulation de la question ou une éventuelle
tentative de manipulation du prompt par l'utilisateur (prompt injection). C'est une limite de
conception volontaire, pas une limite technique de ce qu'un LLM pourrait faire : même si on
pouvait techniquement câbler le modèle à des actions d'écriture, ça ne serait pas fait, parce
qu'un LLM reste par nature manipulable via son entrée en langage naturel, et que le risque d'une
action destructrice mal déclenchée dépasse largement le bénéfice d'automatiser cette action via le
chat.

## Le taux d'erreur mesuré, et ce qu'il enseigne

Sur un jeu de 48 questions de test (36 documentaires, 6 hors périmètre, 6 hors corpus), le
**retrieval seul** (embedding → Qdrant → reranking, sans appeler le LLM) obtient 36/36 — la partie
"recherche" fonctionne bien de façon fiable et déterministe. En revanche, le run **complet** de
bout en bout (Router + RAG + Generator, avec le LLM impliqué à deux endroits) a mesuré 21/48 sur
son unique confirmation complète — soit environ 27 échecs, majoritairement dus à des erreurs de
classification du Router plutôt qu'à des erreurs de recherche. **Ce chiffre 21/48 a été mesuré
avant des corrections ultérieures et n'a jamais été rejoué depuis** (voir `NEXT_SESSION.md`) — à
traiter comme une mesure provisoire, pas comme un résultat définitif à citer tel quel.

Cet écart entre les deux mesures est la démonstration la plus concrète possible d'un principe
général en IA appliquée : **la partie déterministe d'un pipeline (recherche, calcul, règles
codées) est nettement plus fiable et plus facile à faire converger vers 100% que la partie
probabiliste (un LLM qui génère ou classifie du texte)**. Un système hybride bien conçu maximise
la part de travail confiée à du code déterministe et testable, et réduit la surface confiée au LLM
à ce qui ne peut vraiment pas être fait autrement (comprendre du langage naturel, écrire une
réponse en langage naturel).

Pistes explorées pour réduire ce taux d'erreur, non retenues faute de temps ou testées sans effet :
un modèle de classification dédié, plus petit et spécialisé, plutôt qu'un LLM généraliste pour la
seule tâche de routage ; un pré-filtre déterministe (mots-clés/règles) en complément du LLM plutôt
qu'à sa place, pour rattraper les cas évidents avant même d'appeler le modèle. Le taux d'erreur
actuel est documenté comme une limite connue du prototype, assumée en connaissance de cause plutôt
que masquée — le mode de défaillance reste "gracieusement faux" (le système se trompe de
catégorie, mais ne ment jamais avec assurance sur un fait), grâce aux garde-fous structurels
décrits plus haut.
