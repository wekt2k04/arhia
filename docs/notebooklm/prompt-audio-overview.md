# Prompt — Audio Overview (NotebookLM)

*À coller dans le champ "What should the AI hosts focus on in this episode?" (icône crayon à côté
de "Audio Overview", avant de générer). Le porteur du projet a confirmé que la limite réelle du
champ est plus haute que les 500 caractères documentés par Google — le texte ci-dessous en fait
volontairement usage.*

*Version 4 (2026-08-20) : v3 corrigeait 7 faits ; cette passe ne corrige rien de nouveau, elle
resserre le **texte des 46 notions elles-mêmes** (v3 n'avait resserré que les phrases de consigne
autour). Même substance, moins de mots par notion — la liste reste à 46 points, même ordre, rien
omis (la densité de FAITS, pas le nombre de mots du prompt, reste le levier de durée). Seule
exception délibérée : le paragraphe anti-préambule n'a **pas** été raccourci davantage — sa
redondance explicite est precisément ce qui a corrigé l'échec de la v1 (voir rationale plus bas),
le retoucher romprait le seul mécanisme déjà prouvé.*

## Réglages dans l'interface (pas dans le texte du prompt)

Format **Deep Dive**, langue **Français**, longueur **Default** ("Longer" n'existe qu'en anglais).

## Le prompt (à copier tel quel)

```text
Interdiction absolue de preambule : la premiere phrase prononcee EST la notion 1, mot pour mot technique. Bannis sans exception : accroche generique, presentation des hotes, mise en contexte ("aujourd'hui..."), resume du plan, "bienvenue dans cet episode", toute transition sans contenu technique. Zero seconde perdue avant le contenu.

Couvrez EXACTEMENT ces notions, dans cet ordre strict, aucune omise, aucune reorganisee :

FONDATIONS :
1. Architecture hexagonale : 4 couches Domain/Core/Infrastructure/Api, dependance a sens unique.
2. Ports (interfaces Core) / adaptateurs (Infrastructure) : IWorkflowInstanceRepository/WorkflowInstanceRepository, relies dans Program.cs via AddScoped.
3. Program.cs = composition root : seul endroit qui choisit quel adaptateur derriere quel port.
4. Testabilite : logique metier testee avec des doubles (Moq/FluentAssertions), sans infrastructure reelle.
5. RBAC a 3 roles : Employee (ses donnees), HR (son departement), QualityAdmin (portee globale).
6. DepartmentScopeGuard : le role autorise l'action, le departement autorise la cible - verifies independamment (role d'abord dans le code), le role seul ne suffit jamais.
7. Pattern BFF : le navigateur ne parle jamais directement a l'API, JWT jamais expose, cookie httpOnly.
8. JWT stateless : payload, signature, duree de validite, compromis face a la revocation instantanee d'une session classique.

PIPELINE RAG :
9. Probleme resolu par RAG : hallucination et connaissance figee d'un LLM qui repond de memoire.
10. Alternative fine-tuning ecartee : tracabilite, cout de mise a jour, hallucination residuelle.
11. Phase 1 Chunking : decoupage structurel par section Markdown, jamais a taille fixe aveugle.
12. Recouvrement entre chunks voisins : ne pas perdre une info a cheval sur une frontiere.
13. Phase 2 Embedding : vecteur 768 dimensions, modele multilingue, tokenisation SentencePiece (XLM-RoBERTa).
14. ONNX Runtime .NET pur pour l'embedding : aucun appel Ollama sur cette etape, controle total de la latence.
15. Mean-pooling masque + normalisation L2 (MeanPoolAndNormalize) : des vecteurs de tokens a un seul vecteur de phrase.
16. Phase 3 Storage : Qdrant, recherche ANN via HNSW, similarite cosinus plutot que distance brute.
17. Phase 4 Reranking : bi-encodeur (rapide, separe, approximatif) versus cross-encodeur (lent, ensemble, precis).
18. Format de paire RoBERTa pour le reranking, score borne par une fonction sigmoide.
19. Piege reel majeur : un score de reranking eleve (jusqu'a 0.78 observe) ne garantit pas la reponse, seulement une proximite thematique.

ORCHESTRATION CONVERSATIONNELLE :
20. Router puis Generator : deux roles distincts, deux appels au meme modele Ollama local.
21. Ollama local plutot que cloud : souverainete des donnees RH, cout previsible, disponibilite hors ligne.
22. Ce que ce choix coute : capacite de modele plus faible qu'une API cloud de pointe, assume consciemment.
23. Ecart de conception : phi4-mini:3.8b pour les deux roles ; gemma4:12b teste et ecarte, bien trop lent en interactif sans GPU.
24. Prompt engineering du Router : doubler les exemples few-shot teste, aucun effet mesurable sur le taux d'erreur.
25. Fail-safe pas fail-open : sortie du Router validee contre un enum ferme a 3 valeurs, le reste retombe sur HORS_PERIMETRE.
26. Garde-fou anti-hallucination, notion la plus importante du projet : double porte de sortie ecrite en code.
27. Premiere porte : zero candidat retourne par Qdrant, le Generator n'est jamais appele.
28. Seconde porte : aucun candidat au-dessus du seuil de pertinence apres reranking, meme refus, meme non-appel.
29. Cette double porte est testee par des tests unitaires dedies, pas une simple consigne de prompt.
30. Agent strictement informatif : meme un statut de dossier passe par un port lecture seule, jamais d'ecriture via le LLM.
31. Resultats : 211 tests automatises verts (0 warning), logique metier couverte ; gold end-to-end (48 questions, LLM inclus) a 21/48 mais mesure une seule fois avant corrections, jamais rejoue - a presenter comme provisoire.
32. Lecon generale : la partie deterministe d'un systeme est nettement plus fiable que sa partie probabiliste.

DEPLOIEMENT :
33. Docker Compose, 4 services (sqlserver, qdrant, api, frontend), reseau interne resolu par nom de service.
34. Healthcheck reel sur au moins un service (depends_on + condition service_healthy) pour bloquer le demarrage - pas systematique, une autre dependance reste en service_started simple.
35. Multi-stage build : etape de compilation (SDK complet) separee de l'image finale (runtime seul), plus legere.
36. Ordre des COPY dans le Dockerfile : .csproj copies/restaures avant le code source, pour que le cache Docker survive tant que les dependances ne changent pas.
37. Volumes nommes persistants versus bind mount pour les modeles ONNX et le corpus documentaire.
38. Decision explicite de ne PAS conteneuriser Ollama, rejoint depuis le conteneur via host.docker.internal.
39. Migrations de base de donnees automatiques et idempotentes appliquees au demarrage.
40. Secrets (mot de passe SQL, cle JWT) via variables d'environnement, jamais commis dans le depot.

TEMPS REEL ET FLUX METIER :
41. SSE plutot que polling ou WebSocket : flux unidirectionnel serveur vers client, suffisant ici.
42. Piege reel : la compression HTTP integree de Next.js bufferisait tout le flux, cassant le streaming - corrige en la desactivant explicitement sur ce chemin.
43. Trace complete d'une question documentaire : navigateur, Router, pipeline RAG, Generator, fragments SSE recus.
44. Circuit de validation d'un template : Redacteur propose, Verificateur puis Approbateur valident, seul un template approuve instancie un dossier.
45. Onboarding d'un collaborateur : referentiel Poste croise Departement croise Contrat pour determiner les items attendus.
46. Ecart assume, documente plutot que masque : deux flux de logs distincts (technique vs audit trail) prevus dans l'architecture cible, pas encore implementes.

Consacrez nettement plus de temps aux notions 9 a 32 (RAG + orchestration) qu'au reste : c'est le coeur technique du projet. Pour chaque notion, citez le fichier reel exact depuis la racine du depot, jamais seulement le concept general. Developpez avec un exemple ou une consequence concrete plutot que d'enoncer et passer a la suivante : un plan a approfondir, pas des titres a survoler.

Terminez par un recapitulatif oral (30 secondes maximum) : chaque notion, meme ordre, une phrase courte chacune.
```

## Pourquoi ce prompt est écrit ainsi

- **Interdiction de préambule concrète et volontairement redondante** : lister noir sur blanc les
  formulations interdites plutôt que "pas de bienvenue" — la v1 perdait plus d'une minute en
  présentation générique malgré une consigne plus vague. C'est la seule section jamais raccourcie
  depuis, précisément parce qu'elle est déjà la plus efficace du prompt.
- **Aucune durée mentionnée, nulle part** : un chiffre cité risque de devenir un plafond
  psychologique. Le seul levier reste le volume réel de faits distincts — 46 notions denses.
- **Priorité IA/ML quantifiée** : les notions 9 à 32 (24 sur 46) couvrent RAG et orchestration,
  avec consigne explicite d'y consacrer nettement plus de temps.
- **Récapitulatif final exigé**, une phrase par notion — force une couverture vérifiable de la
  liste complète plutôt qu'un abandon en cours de route sur les derniers points.

## Corrections factuelles apportées en v3 (relu contre le vrai code, 2026-08-20)

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
