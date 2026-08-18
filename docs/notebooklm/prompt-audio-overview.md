# Prompt — Audio Overview (NotebookLM)

*À coller dans le champ "What should the AI hosts focus on in this episode?" (icône crayon à côté
de "Audio Overview", avant de générer). Le porteur du projet a confirmé que la limite réelle du
champ est plus haute que les 500 caractères documentés par Google — le texte ci-dessous en fait
volontairement usage.*

*Version 2, après un premier essai réel : l'épisode généré avec la v1 a perdu **plus d'une minute**
en présentation générique avant d'entrer dans le sujet, et a tourné autour de 12 minutes. Deux
correctifs ciblés ici : (1) l'interdiction de préambule est rendue beaucoup plus explicite et
concrète (formulations interdites listées noir sur blanc, pas juste "pas de bienvenue") ; (2)
**aucune durée n'est mentionnée nulle part dans le prompt**, volontairement — citer un chiffre
risquerait de devenir un plafond psychologique. Le seul levier pour allonger l'épisode est le
volume de notions listées ci-dessous, densifié par rapport à la v1.*

## Réglages dans l'interface (pas dans le texte du prompt)

Format **Deep Dive**, langue **Français**, longueur **Default** ("Longer" n'existe qu'en anglais).

## Le prompt (à copier tel quel)

```
Interdiction absolue de preambule. La toute premiere phrase prononcee doit deja etre la notion 1 ci-dessous, mot pour mot technique. Sont interdits, sans exception : accroche generique, presentation des hotes, phrase de mise en contexte ("aujourd'hui on va parler de..."), resume du plan avant de commencer, "bienvenue dans cet episode", toute formule d'ouverture ou de transition qui ne contient aucune information technique. Zero seconde perdue avant le contenu.

Couvrez EXACTEMENT ces notions, dans cet ordre chronologique strict, aucune omise, aucune reorganisee :

FONDATIONS :
1. Architecture hexagonale : 4 couches Domain/Core/Infrastructure/Api, regle de dependance a sens unique.
2. Ports (interfaces Core) et adaptateurs (implementations Infrastructure) : exemple concret IWorkflowInstanceRepository/EfWorkflowInstanceRepository.
3. Program.cs comme composition root : seul endroit qui choisit quel adaptateur brancher derriere quel port.
4. Testabilite : la logique metier testee avec des doubles (Moq/FluentAssertions), sans infrastructure reelle.
5. RBAC a 3 roles : Employee (ses donnees), RH (son pole), QualityAdmin (portee globale).
6. DepartmentScopeGuard : portee verifiee AVANT le role, un RH hors de son pole est refuse quel que soit son role.
7. Pattern BFF : le navigateur ne parle jamais directement a l'API, le JWT n'est jamais expose, cookie httpOnly.
8. JWT stateless : payload, signature, duree de validite, compromis avec la revocation instantanee d'une session classique.

PIPELINE RAG :
9. Le probleme que RAG resout : hallucination et connaissance figee d'un LLM qui repond de memoire.
10. L'alternative fine-tuning et pourquoi elle a ete ecartee (tracabilite, cout de mise a jour, hallucination residuelle).
11. Phase 1 Chunking : decoupage structurel par section Markdown, pas de decoupage a taille fixe aveugle.
12. Le recouvrement entre chunks voisins et pourquoi il existe (ne pas perdre une info a cheval sur une frontiere).
13. Phase 2 Embedding : vecteur de 768 dimensions, modele multilingue, tokenisation SentencePiece/WordPiece.
14. ONNX Runtime .NET pur pour l'embedding : aucun appel a Ollama pour cette etape, controle total de la latence.
15. Mean-pooling et normalisation L2 pour obtenir un seul vecteur de phrase a partir des vecteurs de tokens.
16. Phase 3 Storage : Qdrant, recherche ANN via HNSW, similarite cosinus plutot que distance brute.
17. Phase 4 Reranking : difference entre bi-encodeur (rapide, separe, approximatif) et cross-encodeur (lent, ensemble, precis).
18. Le format de paire RoBERTa pour le reranking et le passage par une fonction sigmoide pour borner le score.
19. Piege reel majeur : un score de reranking eleve, jusqu'a 0.78 observe, ne garantit pas que le chunk contient la reponse, seulement une proximite thematique.

ORCHESTRATION CONVERSATIONNELLE :
20. Router puis Generator : deux roles distincts, deux appels au meme modele Ollama local.
21. Pourquoi Ollama en local plutot qu'une API cloud : souverainete des donnees RH, cout previsible, disponibilite hors ligne.
22. Ce que ce choix coute : capacite de modele plus faible qu'une API cloud de pointe, assume consciemment.
23. Ecart de conception : phi4-mini:3.8b utilise pour les deux roles ; gemma4:12b teste et ecarte car plus de 2 minutes sans reponse sans GPU.
24. Prompt engineering du Router : doubler les exemples few-shot teste empiriquement, aucun effet mesurable sur le taux d'erreur.
25. Fail-safe et non fail-open : la sortie du Router validee contre un enum ferme a 3 valeurs, tout le reste retombe sur HORS_PERIMETRE.
26. Le garde-fou anti-hallucination, la notion la plus importante de tout le projet : double porte de sortie ecrite en code.
27. Premiere porte : zero candidat retourne par la recherche Qdrant, le Generator n'est jamais appele.
28. Seconde porte : aucun candidat au-dessus du seuil de pertinence apres reranking, meme refus, meme non-appel au Generator.
29. Cette double porte est un garde-fou en code teste par des tests unitaires dedies, pas une simple consigne dans un prompt.
30. Agent strictement informatif : meme un statut de dossier passe par un port de lecture seule, jamais d'ecriture declenchee par le LLM.
31. Resultats mesures : 36 sur 36 en retrieval seul sans LLM, 21 sur 48 en bout en bout avec le LLM implique.
32. La lecon generale qui en decoule : la partie deterministe d'un systeme est nettement plus fiable que sa partie probabiliste.

DEPLOIEMENT :
33. Docker Compose, 4 services (sqlserver, qdrant, api, frontend), reseau interne resolu par nom de service.
34. depends_on avec condition de sante (healthcheck) pour eviter les race conditions au demarrage.
35. Multi-stage build : separer l'etape de compilation de l'image finale, plus legere, qui tourne reellement.
36. L'ordre des instructions COPY pour optimiser le cache Docker sur les dependances qui changent peu.
37. Volumes nommes persistants versus bind mount pour les modeles ONNX et le corpus documentaire.
38. Decision explicite de ne PAS conteneuriser Ollama, rejoint depuis le conteneur via host.docker.internal.
39. Migrations de base de donnees automatiques et idempotentes appliquees au demarrage.
40. Secrets (mot de passe SQL, cle JWT) via variables d'environnement, jamais commis dans le depot.

TEMPS REEL ET FLUX METIER :
41. SSE plutot que polling ou WebSocket : flux unidirectionnel serveur vers client, suffisant ici.
42. Piege reel rencontre : la compression HTTP de Next.js bufferisait tout le flux avant de l'envoyer, cassant le streaming.
43. Trace complete d'une question documentaire : du navigateur au Router, au pipeline RAG, au Generator, jusqu'aux fragments SSE recus.
44. Circuit de validation d'un template : Redacteur propose, Verificateur puis Approbateur valident, seul un template approuve peut instancier un dossier.
45. Onboarding d'un collaborateur : resolution du referentiel Poste croise Pole croise Contrat pour determiner les items attendus.
46. Deux flux de logs separes : log technique de debug versus audit trail de conformite, jamais melanges.

Consacrez nettement plus de temps de parole aux notions 9 a 32 (pipeline RAG et orchestration) qu'aux autres blocs : c'est le coeur technique du projet. Pour chaque notion, citez le code source reel et son chemin de fichier exact depuis la racine du depot, jamais seulement le concept general. Developpez chaque notion avec un exemple ou une consequence concrete plutot que de l'enoncer et passer a la suivante : traitez cette liste comme un plan detaille a developper en profondeur, pas comme des titres a survoler.

Terminez par un recapitulatif final oral de 30 secondes maximum qui reprend, notion par notion et dans le meme ordre, chacun des points ci-dessus en une phrase courte chacun.
```

## Pourquoi ce prompt est écrit ainsi

- **Interdiction de préambule rendue concrète** : la v1 disait juste "pas de bienvenue", pas assez
  précis — l'épisode généré a quand même perdu plus d'une minute en présentation générique. La v2
  liste noir sur blanc les formulations interdites et exige que la première phrase soit déjà la
  notion 1, techniquement.
- **Aucune durée mentionnée, nulle part** : citer un chiffre (même "long" ou "30 minutes") risque
  de devenir un plafond psychologique plutôt qu'un plancher. Le seul levier est le volume réel de
  matière à couvrir — 46 notions au lieu de 25 dans la v1, densifiées en sous-points techniques
  précis plutôt que des blocs vagues.
- **Priorité IA/ML quantifiée** : les notions 9 à 32 (24 sur 46, plus de la moitié) couvrent RAG et
  orchestration, avec la consigne explicite d'y consacrer nettement plus de temps.
- **Récapitulatif final demandé explicitement**, une phrase par notion — force une couverture
  vérifiable de la liste complète plutôt qu'un abandon en cours de route sur les derniers points.

## Si le résultat dérive encore

Relancer en ajoutant en tête : `Episode precedent encore trop lent a demarrer ou incomplet,
suivez la liste ci-dessous plus strictement, zero digression non listee ici.`
