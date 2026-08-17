# Prompt — Audio Overview (NotebookLM)

*À coller dans le champ "What should the AI hosts focus on in this episode?" (icône crayon à côté
de "Audio Overview", avant de générer). Ce champ est plafonné à **500 caractères** côté NotebookLM
(confirmé sur la documentation officielle Google, août 2026) — le texte ci-dessous fait 493
caractères, vérifié.*

## Réglages à choisir dans l'interface (pas dans le texte du prompt)

- **Format** : Deep Dive (deux hôtes, conversation approfondie).
- **Langue** : Français.
- **Longueur** : Default — l'option "Longer" n'existe qu'en anglais (contrainte officielle
  NotebookLM), donc pas disponible ici. Le prompt ci-dessous compense en insistant explicitement
  sur la densité et en demandant de ne pas se presser malgré la durée par défaut.

## Le prompt (à copier tel quel)

```
Sources = 5 dossiers techniques d'un projet IA/RAG. Priorite nette aux 2 volets IA (pipeline RAG : chunking, embedding ONNX, Qdrant, reranking cross-encodeur ; orchestration : Router/Generator, garde-fous anti-hallucination) : citez le code ET le chemin de fichier exact de chaque extrait mentionne. Le reste (architecture/securite, Docker, workflows) plus brievement. Un segment par theme, transitions verbales claires. Vu la densite, ne vous pressez pas, quitte a depasser la duree standard.
```

## Pourquoi ce prompt est écrit ainsi

- **Priorité IA/ML explicite** : les documents 02 (pipeline RAG) et 03 (orchestration
  conversationnelle) sont nommément cités avec leurs sous-thèmes précis, pour que les hôtes IA
  passent plus de temps dessus qu'une couverture uniforme des 5 documents ne le ferait par défaut.
- **"citez le code ET le chemin de fichier exact"** : les 5 documents source contiennent désormais
  de vrais extraits de code, chacun annoté avec son chemin complet depuis la racine du dépôt (ex.
  `src/Agirh.Core/Security/RbacMatrix.cs`) — cette instruction demande aux hôtes de nommer ce
  chemin à l'oral à chaque extrait discuté, pas seulement de paraphraser le code. Utile pour
  retrouver immédiatement le bon fichier en réécoutant, sans avoir à deviner de quoi on parle.
- **Pas de demande de "pauses de 5 secondes"** : ce réglage n'existe pas dans NotebookLM (vérifié —
  "Smart Pause" est un contrôle d'écoute côté auditeur, pas un réglage de génération). À la place,
  la structure "un segment par thème, transition verbale claire" est une technique de prompt
  documentée qui produit un effet proche (repères clairs entre sections) avec quelque chose que le
  modèle peut réellement exécuter.
- **Insistance sur la durée** : en l'absence de l'option "Longer" (anglais uniquement), c'est la
  seule manière de pousser vers un épisode long — pas de garantie officielle sur le résultat en
  minutes, mais c'est le levier disponible en français.

## Si le résultat est trop court malgré tout

Relancer une seconde génération en ajoutant en tête du prompt : `Episode precedent trop court,
soyez sensiblement plus exhaustif et prenez davantage de temps sur chaque sous-partie technique.`
(coupe un peu dans le reste du prompt si la limite de 500 caractères est dépassée).
