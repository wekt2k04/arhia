# Prompt — Audio Overview (NotebookLM)

*À coller dans le champ "What should the AI hosts focus on in this episode?" (icône crayon à côté
de "Audio Overview", avant de générer). Le porteur du projet a confirmé que la limite réelle du
champ est plus haute que les 500 caractères documentés par Google — le texte ci-dessous en fait
volontairement usage.*

*Version 3 (2026-08-20) : la v2 fonctionnait (démarrage immédiat, densité correcte) mais deux
choses ont changé depuis sa rédaction. (1) **Le porteur du projet a demandé une version plus
concise** — les phrases d'instruction (interdictions, consignes de pondération, consigne de
récap) sont resserrées, sans toucher au nombre de notions (toujours 46, même ordre, rien omis :
la densité reste le seul levier de durée). (2) **Sept notions se sont révélées inexactes ou
non vérifiées en relisant le vrai code**, corrigées ici plutôt que rediffusées telles quelles —
détail dans "Corrections apportées en v3" plus bas. Toujours aucune durée mentionnée dans le
prompt lui-même, pour la même raison qu'en v2 (éviter un plafond psychologique).*

## Réglages dans l'interface (pas dans le texte du prompt)

Format **Deep Dive**, langue **Français**, longueur **Default** ("Longer" n'existe qu'en anglais).

## Le prompt (à copier tel quel)

```text
Interdiction absolue de preambule : la premiere phrase prononcee EST la notion 1, mot pour mot technique. Bannis sans exception : accroche generique, presentation des hotes, mise en contexte ("aujourd'hui..."), resume du plan, "bienvenue dans cet episode", toute transition sans contenu technique. Zero seconde perdue avant le contenu.

Couvrez EXACTEMENT ces notions, dans cet ordre strict, aucune omise, aucune reorganisee :

FONDATIONS :
1. Architecture hexagonale : 4 couches Domain/Core/Infrastructure/Api, regle de dependance a sens unique.
2. Ports (interfaces Core) et adaptateurs (implementations Infrastructure) : exemple concret IWorkflowInstanceRepository/WorkflowInstanceRepository (Program.cs les relie via AddScoped).
3. Program.cs comme composition root : seul endroit qui choisit quel adaptateur brancher derriere quel port.
4. Testabilite : la logique metier testee avec des doubles (Moq/FluentAssertions), sans infrastructure reelle.
5. RBAC a 3 roles : Employee (ses donnees), HR (son departement), QualityAdmin (portee globale).
6. DepartmentScopeGuard : le role autorise l'action, la portee departement autorise la cible - les deux sont verifies independamment, le role seul ne suffit jamais (ordre reel dans le code : role d'abord, departement ensuite).
7. Pattern BFF : le navigateur ne parle jamais directement a l'API, le JWT n'est jamais expose, cookie httpOnly.
8. JWT stateless : payload, signature, duree de validite, compromis avec la revocation instantanee d'une session classique.

PIPELINE RAG :
9. Le probleme que RAG resout : hallucination et connaissance figee d'un LLM qui repond de memoire.
10. L'alternative fine-tuning et pourquoi elle a ete ecartee (tracabilite, cout de mise a jour, hallucination residuelle).
11. Phase 1 Chunking : decoupage structurel par section Markdown, pas de decoupage a taille fixe aveugle.
12. Le recouvrement entre chunks voisins et pourquoi il existe (ne pas perdre une info a cheval sur une frontiere).
13. Phase 2 Embedding : vecteur de 768 dimensions, modele multilingue, tokenisation SentencePiece (XLM-RoBERTa).
14. ONNX Runtime .NET pur pour l'embedding : aucun appel a Ollama pour cette etape, controle total de la latence.
15. Mean-pooling masque et normalisation L2 (methode MeanPoolAndNormalize) pour obtenir un seul vecteur de phrase a partir des vecteurs de tokens.
16. Phase 3 Storage : Qdrant, recherche ANN via HNSW, similarite cosinus plutot que distance brute.
17. Phase 4 Reranking : difference entre bi-encodeur (rapide, separe, approximatif) et cross-encodeur (lent, ensemble, precis).
18. Le format de paire RoBERTa pour le reranking et le passage par une fonction sigmoide pour borner le score.
19. Piege reel majeur : un score de reranking eleve, jusqu'a 0.78 observe, ne garantit pas que le chunk contient la reponse, seulement une proximite thematique.

ORCHESTRATION CONVERSATIONNELLE :
20. Router puis Generator : deux roles distincts, deux appels au meme modele Ollama local.
21. Pourquoi Ollama en local plutot qu'une API cloud : souverainete des donnees RH, cout previsible, disponibilite hors ligne.
22. Ce que ce choix coute : capacite de modele plus faible qu'une API cloud de pointe, assume consciemment.
23. Ecart de conception : phi4-mini:3.8b utilise pour les deux roles ; gemma4:12b teste et ecarte car bien trop lent pour un usage interactif sans GPU.
24. Prompt engineering du Router : doubler les exemples few-shot teste empiriquement, aucun effet mesurable sur le taux d'erreur.
25. Fail-safe et non fail-open : la sortie du Router validee contre un enum ferme a 3 valeurs, tout le reste retombe sur HORS_PERIMETRE.
26. Le garde-fou anti-hallucination, la notion la plus importante de tout le projet : double porte de sortie ecrite en code.
27. Premiere porte : zero candidat retourne par la recherche Qdrant, le Generator n'est jamais appele.
28. Seconde porte : aucun candidat au-dessus du seuil de pertinence apres reranking, meme refus, meme non-appel au Generator.
29. Cette double porte est un garde-fou en code teste par des tests unitaires dedies, pas une simple consigne dans un prompt.
30. Agent strictement informatif : meme un statut de dossier passe par un port de lecture seule, jamais d'ecriture declenchee par le LLM.
31. Resultats mesures : 211 tests automatises verts (0 warning au build) sur toute la logique metier ; le jeu de questions-reponses gold end-to-end (48 questions, LLM inclus) a ete mesure une fois a 21/48 avant corrections, jamais rejoue depuis - a formuler comme mesure provisoire, pas comme chiffre final.
32. La lecon generale qui en decoule : la partie deterministe d'un systeme est nettement plus fiable que sa partie probabiliste.

DEPLOIEMENT :
33. Docker Compose, 4 services (sqlserver, qdrant, api, frontend), reseau interne resolu par nom de service.
34. Healthcheck reel sur au moins un service, utilise en depends_on via condition service_healthy pour bloquer le demarrage tant qu'il n'est pas pret - toutes les dependances n'utilisent pas ce niveau de garantie (certaines restent en service_started simple).
35. Multi-stage build : separer l'etape de compilation (SDK complet) de l'image finale d'execution (runtime seul), plus legere.
36. L'ordre des instructions COPY dans le Dockerfile : les .csproj copies et restaures avant le reste du code source, pour que ce cache Docker survive tant que les dependances ne changent pas.
37. Volumes nommes persistants versus bind mount pour les modeles ONNX et le corpus documentaire.
38. Decision explicite de ne PAS conteneuriser Ollama, rejoint depuis le conteneur via host.docker.internal.
39. Migrations de base de donnees automatiques et idempotentes appliquees au demarrage.
40. Secrets (mot de passe SQL, cle JWT) via variables d'environnement, jamais commis dans le depot.

TEMPS REEL ET FLUX METIER :
41. SSE plutot que polling ou WebSocket : flux unidirectionnel serveur vers client, suffisant ici.
42. Piege reel rencontre : la compression HTTP integree de Next.js bufferisait tout le flux avant de l'envoyer, cassant le streaming - corrige en desactivant explicitement la compression sur ce chemin.
43. Trace complete d'une question documentaire : du navigateur au Router, au pipeline RAG, au Generator, jusqu'aux fragments SSE recus.
44. Circuit de validation d'un template : Redacteur propose, Verificateur puis Approbateur valident, seul un template approuve peut instancier un dossier.
45. Onboarding d'un collaborateur : resolution du referentiel Poste croise Departement croise Contrat pour determiner les items attendus.
46. Ecart de conception assume, documente plutot que masque : deux flux de logs distincts (technique de debug vs audit trail de conformite) sont prevus dans l'architecture cible mais pas encore implementes a ce jour.

Consacrez nettement plus de temps aux notions 9 a 32 (RAG + orchestration) qu'au reste : c'est le coeur technique du projet. Pour chaque notion, citez le fichier reel exact depuis la racine du depot, jamais seulement le concept general. Developpez avec un exemple ou une consequence concrete plutot que d'enoncer et passer a la suivante : c'est un plan a approfondir, pas des titres a survoler.

Terminez par un recapitulatif oral (30 secondes maximum) : chaque notion, meme ordre, une phrase courte chacune.
```

## Pourquoi ce prompt est écrit ainsi

- **Interdiction de préambule concrète** : lister noir sur blanc les formulations interdites plutôt
  que "pas de bienvenue" — la v1 perdait plus d'une minute en présentation générique malgré une
  consigne plus vague.
- **Aucune durée mentionnée, nulle part** : un chiffre cité risque de devenir un plafond
  psychologique. Le seul levier reste le volume réel de matière — 46 notions denses.
- **Priorité IA/ML quantifiée** : les notions 9 à 32 (24 sur 46) couvrent RAG et orchestration,
  avec consigne explicite d'y consacrer nettement plus de temps.
- **Récapitulatif final exigé**, une phrase par notion — force une couverture vérifiable de la
  liste complète plutôt qu'un abandon en cours de route sur les derniers points.

## Corrections apportées en v3 (relu contre le vrai code, 2026-08-20)

- **#2** : la classe réelle est `WorkflowInstanceRepository`, pas "EfWorkflowInstanceRepository"
  (préfixe `Ef` inexistant dans ce dépôt) — vérifié dans
  `src/Agirh.Infrastructure/Persistence/Repositories/WorkflowInstanceRepository.cs`.
- **#6** : la v2 affirmait la portée département vérifiée *avant* le rôle. C'est l'inverse dans
  le code réel (`CreateEmployeeRecordUseCase.cs`, `InstantiateWorkflowUseCase.cs` et 4 autres use
  cases) : `RbacMatrix.IsAuthorized` (rôle) est toujours appelé avant
  `DepartmentScopeGuard.Can...` (portée). Le point important reste vrai (le rôle seul ne suffit
  jamais) mais l'ordre affirmé était faux — corrigé.
- **#13** : le tokenizer réel est `SentencePieceTokenizer` (`XlmRobertaTokenizer.cs`) — jamais de
  WordPiece dans ce dépôt, retiré de la formulation.
- **#23** : le ">2 minutes" pour `gemma4:12b` n'est confirmé nulle part dans le HANDOFF (seul
  "trop lent sans GPU" y est écrit) — chiffre retiré plutôt que répété sans preuve ; à confirmer
  de mémoire si besoin en soutenance.
- **#31** : "36 sur 36" n'est confirmé nulle part et "21 sur 48" est explicitement noté comme
  *"avant corrections, jamais rejoué depuis"* dans `NEXT_SESSION.md` — présenté maintenant comme
  mesure provisoire, avec le chiffre solide (211 tests automatisés) mis en avant à la place.
- **#34** : `docker-compose.yml` a un vrai `healthcheck` utilisé en `condition: service_healthy`
  pour au moins une dépendance, mais une autre dépendance du même bloc reste en
  `condition: service_started` (pas de healthcheck) — plus précis et plus intéressant que
  l'affirmation générique de la v2.
- **#46** : `IAuditTrailPort`, `AuditTrailAdapter` et `TechnicalLogAdapter` (mentionnés dans
  `ARCHITECTURE.md`) n'existent dans aucun fichier de `src/` (vérifié le 2026-08-19) — la v2
  décrivait ce flux séparé comme s'il existait déjà. Reformulé en écart de conception assumé,
  cohérent avec le ton du reste du projet ("documenté plutôt que masqué").
- Terminologie : "pôle" (vocabulaire pré-migration) remplacé par "département" partout où il
  restait (#5, #6, #45).

## Si le résultat dérive encore

Relancer en ajoutant en tête : `Episode precedent encore trop lent a demarrer ou incomplet,
suivez la liste ci-dessous plus strictement, zero digression non listee ici.`
