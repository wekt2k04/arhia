# Prompt — Audio Overview (NotebookLM)

*À coller dans le champ "What should the AI hosts focus on in this episode?" (icône crayon à côté
de "Audio Overview", avant de générer). **Limite réelle du champ mesurée empiriquement le
2026-08-20 : 5000 caractères pile** — un collage de la v4 (6486 caractères) a été tronqué net en
plein mot par NotebookLM, sans avertissement. Ce n'est plus "plus haut que 500" (estimation d'un
essai antérieur avec un prompt plus court) : c'est un chiffre confirmé. Le bloc ci-dessous fait
4893 caractères — sous la limite avec ~107 caractères de marge pour absorber un éventuel écart de
comptage entre éditeurs.*

*Version 5 (2026-08-20) : la v4 (46 notions resserrées) dépassait quand même 6486 caractères,
donc tronquée par NotebookLM avant la fin de DEPLOIEMENT. Cette passe coupe ~1600 caractères
supplémentaires pour repasser sous la limite réelle : formulation encore plus télégraphique par
notion, sections FONDATIONS/PIPELINE RAG/etc. retirées (les notions restent groupées et numérotées
dans le même ordre, juste sans étiquette de section — elles ne servaient qu'au confort de lecture
humaine, pas à l'instruction elle-même), et le paragraphe anti-préambule légèrement raccourci en
dernier recours (la phrase de clôture "Zero seconde perdue" a été retirée : redondante avec
"Interdiction... premiere phrase EST notion 1" qui porte déjà le même mandat ; chaque formulation
interdite listée, elle, reste intacte). Toujours 46 notions, même ordre, rien omis.*

## Réglages dans l'interface (pas dans le texte du prompt)

Format **Deep Dive**, langue **Français**, longueur **Default** ("Longer" n'existe qu'en anglais).

## Le prompt (à copier tel quel)

```text
Interdiction preambule : premiere phrase EST notion 1, mot pour mot technique. Bannis : accroche generique, presentation hotes, mise en contexte ("aujourd'hui..."), resume du plan, "bienvenue", transition sans contenu technique.

Couvrez EXACTEMENT ces notions, ordre strict, aucune omise, aucune reorganisee :

1. Architecture hexagonale : 4 couches Domain/Core/Infrastructure/Api, dependance sens unique.
2. Ports/adaptateurs : IWorkflowInstanceRepository/WorkflowInstanceRepository, AddScoped dans Program.cs.
3. Program.cs = composition root : choisit l'adaptateur par port.
4. Testabilite : logique metier testee avec doubles (Moq/FluentAssertions), sans infrastructure reelle.
5. RBAC 3 roles : Employee (donnees), HR (departement), QualityAdmin (portee globale).
6. DepartmentScopeGuard : portee verifiee en plus du role (role d'abord code), jamais suffisant seul.
7. Pattern BFF : navigateur jamais direct API, JWT jamais expose, cookie httpOnly.
8. JWT stateless : payload, signature, duree validite, compromis vs revocation instantanee d'une session classique.

9. Probleme resolu par RAG : hallucination et connaissance figee d'un LLM qui repond de memoire.
10. Alternative fine-tuning ecartee : tracabilite, cout mise a jour, hallucination residuelle.
11. Phase 1 Chunking : decoupage structurel par section Markdown, jamais taille fixe aveugle.
12. Recouvrement entre chunks voisins : ne pas perdre une info a cheval sur une frontiere.
13. Phase 2 Embedding : vecteur 768 dimensions, modele multilingue, tokenisation SentencePiece.
14. ONNX Runtime .NET pur pour l'embedding : aucun appel Ollama, controle total latence.
15. Mean-pooling masque + normalisation L2 (MeanPoolAndNormalize) : tokens vers un vecteur de phrase.
16. Phase 3 Storage : Qdrant, recherche ANN via HNSW, similarite cosinus vs distance brute.
17. Phase 4 Reranking : bi-encodeur (rapide, approximatif) vs cross-encodeur (lent, precis).
18. Format paire RoBERTa pour reranking, score borne par sigmoide.
19. Piege reel majeur : score reranking eleve (jusqu'a 0.78 observe) ne garantit pas la reponse, juste proximite thematique.

20. Router puis Generator : deux roles distincts, deux appels au meme modele Ollama local.
21. Ollama local vs cloud : souverainete donnees RH, cout previsible, dispo hors ligne.
22. Cout du choix : capacite de modele plus faible qu'une API cloud de pointe, assume.
23. Ecart conception : phi4-mini:3.8b pour les deux roles ; gemma4:12b teste et ecarte, trop lent sans GPU.
24. Prompt engineering Router : doubler few-shot teste, aucun effet mesurable sur taux d'erreur.
25. Fail-safe pas fail-open : sortie Router validee vs enum ferme 3 valeurs, reste = HORS_PERIMETRE.
26. Garde-fou anti-hallucination, notion la plus importante : double porte sortie ecrite en code.
27. Premiere porte : zero candidat retourne par Qdrant, Generator jamais appele.
28. Seconde porte : aucun candidat au-dessus du seuil pertinence apres reranking, meme refus/non-appel.
29. Double porte testee par tests unitaires dedies, pas simple consigne de prompt.
30. Agent strictement informatif : statut dossier via port lecture seule, jamais ecriture par le LLM.
31. Resultats : 211 tests verts, 0 warning ; gold end-to-end 21/48, provisoire jamais reconfirme.
32. Lecon generale : partie deterministe d'un systeme plus fiable que sa partie probabiliste.

33. Docker Compose, 4 services (sqlserver, qdrant, api, frontend), reseau resolu par nom.
34. Healthcheck reel (depends_on + service_healthy) bloque demarrage d'un service, pas tous.
35. Multi-stage build : compilation (SDK complet) separee de l'image finale (runtime), plus legere.
36. Ordre COPY Dockerfile : .csproj restaures avant code source, cache Docker survit tant que peu change.
37. Volumes persistants vs bind mount pour modeles ONNX et corpus documentaire.
38. Decision : pas conteneuriser Ollama, rejoint via host.docker.internal.
39. Migrations base automatiques et idempotentes appliquees au demarrage.
40. Secrets (SQL, JWT) via variables d'environnement, jamais commis.

41. SSE vs polling/WebSocket : flux unidirectionnel serveur vers client, suffisant ici.
42. Piege reel : compression HTTP Next.js bufferisait le flux, cassant streaming - corrige en desactivant.
43. Trace question documentaire : navigateur, Router, pipeline RAG, Generator, fragments SSE.
44. Circuit template : Redacteur propose, Verificateur/Approbateur valident, seul approuve instancie.
45. Onboarding : referentiel Poste x Departement x Contrat determine items attendus.
46. Deux flux de logs prevus (technique vs audit trail), pas implementes - ecart assume, documente.

Notions 9-32 (RAG+orchestration) : nettement plus de temps, coeur technique. Citez le fichier reel exact par notion, developpez avec exemple/consequence concrete - approfondir, pas survoler.

Terminez par recapitulatif oral (30s max) : chaque notion, meme ordre, une phrase courte.
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
