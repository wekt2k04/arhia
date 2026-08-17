# Prompt — Quiz (NotebookLM)

*À coller dans le champ de prompt personnalisé lors de la génération d'un quiz (icône d'édition à
côté de "Quiz"). Choisir la difficulté **Difficile** dans l'interface — ce prompt est conçu pour ce
niveau, un quiz Facile/Moyen avec ce texte donnerait des questions mal calibrées.*

## Le prompt (à copier tel quel)

```
Genere un quiz technique couvrant les 5 documents source, avec une repartition privilegiant les deux volets IA/ML (pipeline RAG et orchestration conversationnelle) sans negliger architecture/securite, Docker et workflows/temps reel.

Priorites de contenu, par ordre :
1. Regles metier et garde-fous explicitement encodes en code (pas de simples definitions) : RBAC/PoleScopeGuard, seuil de pertinence RAG, garde-fou anti-hallucination (double porte de sortie), fail-safe du Router, contraintes de circuit (template Approuve requis, dossier archive = lecture seule).
2. Syntaxe et signatures de code reelles citees dans les sources (noms de classes/methodes, format des donnees, valeurs de constantes) - pas seulement les concepts generaux autour.
3. Choix d'architecture et leurs justifications (pourquoi ce choix plutot qu'une alternative plausible).

Consignes :
- Questions a choix multiples avec un distracteur plausible base sur une confusion frequente relevee dans les sources (ex. RBAC pur vs verification de portee, embedding vs reranking, volume vs volume attache).
- Niveau de difficulte eleve : viser la comprehension precise, pas la reconnaissance superficielle de mots-cles.
- Pour chaque question portant sur un extrait de code, inclure dans l'explication de la reponse le chemin de fichier complet indique dans la source (ex. src/Agirh.Core/Security/RbacMatrix.cs), pas seulement le nom de la classe.
- Pour chaque question, la reponse doit etre verifiable dans le texte source, jamais une extrapolation.
```

## Pourquoi ce prompt est écrit ainsi

- **Même logique de pondération que le prompt audio** (voir `prompt-audio-overview.md`) : plus de
  questions sur RAG/orchestration, sans exclure le reste.
- **Priorité 1 (règles/garde-fous) avant priorité 2 (syntaxe)** : teste d'abord la compréhension du
  *pourquoi* et du *comportement*, avant la mémorisation de noms précis — cohérent avec le style
  pédagogique déjà présent dans les 5 documents source ("pourquoi", pas juste "quoi").
- **Distracteurs basés sur des confusions réelles** : les documents source signalent déjà plusieurs
  pièges concrets (score de reranking ≠ présence de la réponse, RBAC pur insuffisant sans
  `PoleScopeGuard`...) — ce sont exactement les bases d'un bon distracteur, plutôt que des réponses
  fausses arbitraires faciles à écarter par élimination.

## Variante — quiz ciblé sur un seul document

Pour un quiz plus court, ciblé sur un seul thème (ex. juste le pipeline RAG), remplacer la première
ligne par : `Genere un quiz technique portant uniquement sur le document "02-ia-pipeline-rag.md"`
et garder le reste du prompt tel quel.
