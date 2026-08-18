# Prompt — Flashcards (NotebookLM)

*À coller dans le champ de prompt personnalisé lors de la génération de flashcards (icône
d'édition à côté de "Flashcards"). Réglages à choisir dans l'interface, pas dans le texte :
**type de carte = Question/Réponse** (pas "Définition" ni "Cloze deletion" — ce prompt est écrit
pour ce format), **difficulté = Difficile**, **nombre de cartes = Plus** (le sujet est dense,
"Standard" couvrirait mal 5 documents).*

## Le prompt (à copier tel quel)

```
Genere des flashcards Question/Reponse couvrant les 5 documents source, avec une repartition privilegiant les deux volets IA/ML (pipeline RAG et orchestration conversationnelle) sans negliger architecture/securite, Docker et workflows/temps reel.

Priorites de contenu, par ordre :
1. Regles metier et garde-fous explicitement encodes en code (pas de simples definitions) : RBAC/DepartmentScopeGuard, seuil de pertinence RAG, garde-fou anti-hallucination (double porte de sortie), fail-safe du Router, contraintes de circuit (template Approved requis, dossier archive = lecture seule).
2. Syntaxe et signatures de code reelles citees dans les sources (noms de classes/methodes, format des donnees, valeurs de constantes) - pas seulement les concepts generaux autour.
3. Choix d'architecture et leurs justifications (pourquoi ce choix plutot qu'une alternative plausible).

Format de chaque carte :
- Face avant : une question precise et autonome (comprehensible sans le contexte des autres cartes), jamais une simple definition a completer.
- Face arriere : la reponse complete, et si elle porte sur un extrait de code, le chemin de fichier complet indique dans la source (ex. src/Agirh.Core/Security/RbacMatrix.cs), pas seulement le nom de la classe.
- Niveau de difficulte eleve : viser la comprehension precise et le "pourquoi", pas la reconnaissance superficielle de mots-cles.
- La reponse doit etre verifiable dans le texte source, jamais une extrapolation.
```

## Pourquoi ce prompt est écrit ainsi

- **Même logique de pondération que le prompt audio** (voir `prompt-audio-overview.md`) : plus de
  cartes sur RAG/orchestration, sans exclure le reste.
- **Type de carte Question/Réponse plutôt que Définition ou Cloze** : une carte "Définition" teste
  la mémorisation d'un terme, une carte "Cloze" (texte à trous) teste le rappel exact d'un mot —
  aucun des deux ne convient bien pour tester "pourquoi ce garde-fou existe" ou "que se passe-t-il
  si..." ; Question/Réponse permet une vraie question de compréhension, cohérent avec la priorité 1
  du prompt (règles/garde-fous, pas de simples définitions).
- **Priorité 1 (règles/garde-fous) avant priorité 2 (syntaxe)** : teste d'abord la compréhension du
  *pourquoi* et du *comportement*, avant la mémorisation de noms précis — cohérent avec le style
  pédagogique déjà présent dans les 5 documents source ("pourquoi", pas juste "quoi").
- **Chemin de fichier sur la face arrière, pas la face avant** : la question doit rester lisible
  seule (une face avant qui contient déjà le chemin du fichier trahit souvent la réponse) ; le
  chemin sert à vérifier/retrouver la source une fois la réponse connue, pas à deviner la question.

## Variante — flashcards ciblées sur un seul document

Pour un jeu plus court, ciblé sur un seul thème (ex. juste le pipeline RAG), remplacer la première
ligne par : `Genere des flashcards Question/Reponse portant uniquement sur le document
"02-ia-pipeline-rag.md"` et garder le reste du prompt tel quel.
