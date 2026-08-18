# Script orateur — Soutenance de stage AGIRH

> Ce que tu dis, slide par slide. Français parlé, première personne, ~15 min (marge avant les
> limites habituelles d'une soutenance de stage). Conseils : parle lentement, marque une pause
> après chaque titre de slide, regarde le jury (pas l'écran). Les mots en **gras** sont à appuyer.
> `[clic]` = déclenche une animation/transition si tu en ajoutes dans PowerPoint.

**Minutage cible total : ~15 min** · Slide 1 → 20.

*Ce script correspond au contenu réel et vérifié du projet (211/211 tests, pipeline RAG et
orchestration fonctionnels de bout en bout) — pas à une version antérieure du projet.*

> **Le fichier `AGIRH_Soutenance.pptx` correspondant existe dans ce même dossier** mais n'est pas
> commité (binaire, non-diffable — voir `.gitignore`). Pour le regénérer : `python
> docs/presentations/generate_pptx.py` (nécessite `pip install python-pptx`, et les logos AGIRH/ENSA
> Safi dans `C:\Users\Wilfried\OneDrive\Bureau\presentation\assets\`, hors dépôt).

---

### Slide 1 — Page de titre *(~35 s)*
« Bonjour à toutes et à tous. Je suis **Wilfried TSETSE**, élève-ingénieur en 2ᵉ année Génie
Informatique de Données et IA à l'**ENSA Safi**. Je vous présente aujourd'hui le projet mené
durant mon stage chez **AGIRH**, sous le tutorat de Monsieur **Issam MITAR** : la réalisation d'un
**assistant RH agentique** pour l'onboarding et l'offboarding des collaborateurs. »

> *Transition :* « Voici le plan de cette présentation. »

---

### Slide 2 — Agenda *(~25 s)*
« Ma présentation suit quatre temps. **Un** : le contexte et le besoin métier — pourquoi cet
agent, pour qui, avec quelles règles. **Deux** : l'architecture technique retenue. **Trois**, et
c'est le cœur de mon travail, **le sous-système IA** — le pipeline RAG et l'orchestration
conversationnelle. **Quatre** : les résultats mesurés, le déploiement, et les perspectives.
Comptez environ quinze minutes, puis je serai à votre disposition pour vos questions. »

> *Transition :* « Entrons dans le contexte. »

---

### Slide 3 — Séparateur Partie 1 *(~8 s)*
« **Partie 1 — Contexte et besoin métier.** »

---

### Slide 4 — Le constat *(~55 s)*
« L'intégration et le départ d'un collaborateur sont deux phases critiques : elles mobilisent
trois acteurs — les **RH**, l'**IT** et le **management** — et elles restent aujourd'hui largement
manuelles, donc lentes et sources d'oublis. Le contexte réel du projet ajoute une contrainte
supplémentaire : une exigence de **conformité qualité**, inspirée des checklists internes de
l'entreprise. `[clic]` L'objectif est donc un agent conversationnel qui **guide** — répond aux
questions sur les politiques internes — et **informe** — renseigne sur l'avancement d'un dossier.
`[clic]` Et j'insiste sur un point de conception central : cet agent ne fait **jamais** d'action à
la place des RH. Il ne remplace jamais le jugement humain. »

> *Transition :* « Voyons plus précisément le périmètre retenu. »

---

### Slide 5 — Périmètre & rôles *(~65 s)*
« Le périmètre est volontairement resserré à **l'onboarding et l'offboarding uniquement** — pas de
congés, pas de paie. C'est une décision assumée : mieux vaut un socle solide sur un périmètre
maîtrisé qu'un prototype large et fragile.
`[clic]` Trois rôles structurent tout le système, à portée croissante. Le **Collaborateur** ne voit
que ses propres données. Le **RH** gère les collaborateurs de **son pôle uniquement** — et cette
vérification de portée est faite **avant même** la vérification du rôle. L'**Admin/Qualité** a une
portée globale et est seul habilité à élever un compte ou valider un template.
`[clic]` En bas, le circuit qualité de validation d'un template de checklist : un RH le **propose**,
un Admin/Qualité le **vérifie**, un autre — ou le même en pratique — l'**approuve**. Point
important : seul un template **approuvé** peut servir à instancier un dossier réel, et cette
contrainte est vérifiée **par le code**, pas seulement documentée. »

> *Transition :* « Ces rôles et ce périmètre s'incarnent dans une vision précise de l'agent. »

---

### Slide 6 — La vision *(~55 s)*
« Chaque question posée dans le chat passe d'abord par un **Router**, qui classe l'intention.
`[clic]` Si la question est **documentaire** — une question générale sur une politique — elle
déclenche le pipeline RAG, que je détaillerai en partie 3. `[clic]` Si elle porte sur le
**statut d'un dossier personnel**, elle déclenche une simple lecture — jamais une recherche
documentaire. `[clic]` Et si elle est **hors périmètre**, l'agent refuse poliment.
Dans les deux premiers cas, j'insiste : le chemin de code passe **systématiquement** par un port de
**lecture seule**. Aucune écriture n'est jamais déclenchée par le LLM — c'est une limite de
conception volontaire, pas une limite technique. »

> *Transition :* « Voyons maintenant comment cette vision se traduit techniquement. »

---

### Slide 7 — Séparateur Partie 2 *(~8 s)*
« **Partie 2 — Architecture technique.** »

---

### Slide 8 — Architecture hexagonale *(~70 s)*
« J'ai retenu une **architecture hexagonale**, dite ports et adaptateurs. Le code est rangé en
quatre couches, avec une règle unique : une couche ne dépend **que** de celles listées en dessous
d'elle, jamais l'inverse.
`[clic]` **Agirh.Domain**, au centre, contient les entités pures — zéro dépendance externe, il ne
sait même pas qu'une base de données existe. `[clic]` **Agirh.Core** définit les ports — des
interfaces — et la logique métier elle-même : les use cases, le RBAC. `[clic]` **Agirh.Infrastructure**
fournit un adaptateur concret par port : EF Core, Qdrant, ONNX, Ollama. `[clic]` Et **Agirh.Api**
est la composition root — le seul endroit qui décide quel adaptateur brancher derrière chaque port.
Le bénéfice concret : remplacer SQL Server, Qdrant ou Ollama ne touche **que** la couche
Infrastructure. Le métier ne bouge pas. Et la logique métier se teste sans la moindre infrastructure
réelle — c'est ce qui rend possible les 211 tests que je vous montrerai en partie 4. »

> *Transition :* « Voici la stack complète qui implémente cette architecture. »

---

### Slide 9 — Stack technique *(~50 s)*
« Rapidement, la stack complète. Backend en **.NET 8** avec ASP.NET Core et EF Core. Frontend en
**Next.js 15**, en pattern BFF, avec Tailwind et shadcn/ui. Authentification par **JWT** et ASP.NET
Identity. Deux bases de données aux rôles distincts : **SQL Server** pour le relationnel, **Qdrant**
pour le vectoriel — le cœur du RAG. Côté IA : **ONNX Runtime** en .NET pur pour l'embedding et le
reranking, et **Ollama**, en local, pour la génération. Le temps réel passe entièrement par **SSE**.
Et le déploiement se fait via **Docker Compose**, quatre services. »

> *Transition :* « Sur ce socle, la sécurité était une priorité — voyons comment elle est traitée. »

---

### Slide 10 — Sécurité *(~60 s)*
« Deux mécanismes structurent la sécurité. `[clic]` D'abord, le **RBAC à portée** : au-delà du
simple rôle, `DepartmentScopeGuard` vérifie qu'un RH cible bien un dossier de **son** pôle — et cette
vérification est faite **avant** même de regarder le rôle. Un RH qui sort de son pôle est refusé,
point final.
`[clic]` Ensuite, le pattern **BFF** : le navigateur ne parle **jamais** directement à l'API .NET.
Il passe par Next.js, qui appelle l'API côté serveur avec le JWT, puis pose un **cookie httpOnly**
— totalement invisible en JavaScript. Même un script XSS injecté ne peut pas le lire. Et côté
validation JWT, les quatre vérifications — émetteur, audience, durée de vie, signature — sont
toutes actives, ce qui n'est pas toujours le cas dans un exemple pris à la légère. »

> *Transition :* « Nous arrivons maintenant au cœur de mon travail : le sous-système IA. »

---

### Slide 11 — Séparateur Partie 3 *(~10 s)*
« **Partie 3 — Le cœur IA : pipeline RAG et orchestration conversationnelle.** C'est la partie la
plus dense de mon travail, je vais prendre un peu plus de temps dessus. »

---

### Slide 12 — Pipeline RAG (1/2) *(~65 s)*
« Le RAG — Retrieval-Augmented Generation — répond à un problème précis : au lieu de laisser un LLM
répondre **de mémoire**, donc risquer l'hallucination, on va chercher les vrais passages dans la
documentation, et on force le modèle à ne parler **que** de ça.
`[clic]` **Phase 1, le chunking** : je découpe les documents **par structure** — les sections
Markdown — et **avec recouvrement** entre chunks voisins, pour ne jamais perdre une information à
cheval sur une frontière de découpage. `[clic]` **Phase 2, l'embedding** : chaque chunk devient un
vecteur de 768 dimensions, via un modèle **multilingue** exécuté en **ONNX Runtime .NET pur** — pas
d'appel à Ollama pour cette étape, ce qui me donne un contrôle total sur la latence.
Voici le code réel du constructeur du chunker, avec ses valeurs par défaut : 400 tokens maximum par
chunk, 15% de recouvrement. »

> *Transition :* « Les vecteurs obtenus doivent ensuite être cherchés, puis affinés. »

---

### Slide 13 — Pipeline RAG (2/2) *(~70 s)*
« `[clic]` **Phase 3, le storage** : les vecteurs vivent dans **Qdrant**, indexés en HNSW pour une
recherche approximative rapide, en similarité cosinus. `[clic]` **Phase 4, le reranking** — la
phase la plus déterminante, et souvent la plus négligée dans un RAG basique. La différence clé :
l'embedding compare requête et document **séparément**, rapidement mais de façon approximative ; le
reranking, lui, est un **cross-encodeur** qui les lit **ensemble**, plus lentement mais avec une
précision nettement supérieure.
J'insiste sur un piège réel que j'ai rencontré et que je documente explicitement : un score de
reranking élevé — jusqu'à 0.78 observé — ne garantit **pas** que le chunk contient la réponse,
seulement qu'il est **thématiquement proche**. Le score mesure une proximité, pas une vérité. C'est
une leçon que je retiens au-delà de ce projet : ne jamais confondre un score de similarité avec une
preuve de correction. »

> *Transition :* « Une fois l'information trouvée, encore faut-il décider quand l'utiliser. C'est le
> rôle de l'orchestration. »

---

### Slide 14 — Orchestration *(~65 s)*
« L'orchestration, ce sont deux rôles distincts, deux appels à Ollama. `[clic]` Le **Router**
classifie l'intention de la question. Point essentiel : sa sortie brute n'est **jamais** utilisée
telle quelle — elle est validée contre un enum fermé à trois valeurs, et tout ce qui ne correspond
pas exactement — une sortie vide, un timeout, un mot halluciné — retombe **par défaut** sur
`HORS_PERIMETRE`. C'est un système **fail-safe**, pas fail-open : un flou ou une panne du modèle ne
peut jamais accidentellement ouvrir l'accès à quelque chose.
`[clic]` Le **Generator**, lui, reçoit uniquement le contexte déjà filtré et écrit la réponse en
streaming, fragment par fragment, via SSE.
Je suis transparent sur une limite mesurée : environ **27% de mauvais classement** sur un jeu de 48
questions de test. J'ai testé une piste d'amélioration — doubler les exemples dans le prompt du
Router — sans **aucun effet mesurable**. La capacité d'un petit modèle à suivre des règles
explicites semble plafonner, et plus de contexte ne compense pas cette limite. »

> *Transition :* « Ce taux d'erreur du Router m'amène directement au garde-fou le plus important de
> tout le projet. »

---

### Slide 15 — Le garde-fou anti-hallucination *(~75 s)*
« Voici, à mon sens, le point le plus important de toute ma présentation. Le système comporte
**deux portes de sortie anticipée**, avant même d'envisager d'appeler le Generator.
`[clic]` **Première porte** : si la recherche Qdrant retourne **zéro candidat**, le Generator n'est
**jamais appelé** — refus immédiat. `[clic]` **Seconde porte** : même si Qdrant retourne des
résultats, si **aucun** ne passe le seuil de pertinence après reranking, même refus, même absence
d'appel au Generator.
Ce n'est pas une consigne dans un prompt que le modèle pourrait ignorer sur une question ambiguë —
c'est un garde-fou **écrit en code**, vérifiable, et couvert par des tests unitaires dédiés. C'est
exactement ce qui rend le mode de défaillance du système **« gracieusement faux »** — il peut se
tromper de catégorie de temps en temps, mais il ne ment **jamais** avec assurance sur un fait. »

> *Transition :* « Voyons maintenant si tout cela fonctionne réellement, en conditions mesurées. »

---

### Slide 16 — Séparateur Partie 4 *(~8 s)*
« **Partie 4 — Résultats, déploiement et perspectives.** »

---

### Slide 17 — Résultats mesurés *(~60 s)*
« Les chiffres. `[clic]` **211 tests automatisés**, tous verts, **zéro warning** au build. `[clic]`
Les **quatre phases** du pipeline RAG sont toutes obligatoires — aucune n'est optionnelle, c'est un
choix de conception assumé. `[clic]` Et le **27%** d'erreurs de routage, déjà mentionné, documenté
comme une limite connue plutôt que masquée.
Concrètement, j'ai vérifié en HTTP réel, de bout en bout : une question posée dans le navigateur
produit une réponse **sourcée**, qui arrive **token par token** en streaming. Et j'insiste encore
une fois : l'anti-hallucination n'est pas qu'un principe — elle est vérifiée par des tests unitaires
dédiés qui la couvrent explicitement. »

> *Transition :* « Tout ce système tourne aujourd'hui de façon reproductible, grâce au
> déploiement. »

---

### Slide 18 — Déploiement *(~55 s)*
« Quatre services orchestrés par **Docker Compose** : `sqlserver` pour le relationnel, `qdrant`
pour le vectoriel, `api` pour le backend .NET, `frontend` pour Next.js. Une seule commande démarre
toute la pile. Point notable : **Ollama reste natif** sur la machine hôte, jamais conteneurisé — un
choix assumé, puisqu'il était déjà installé et utilisé, et que le conteneur `api` le rejoint
simplement via `host.docker.internal`.
Les migrations de base de données s'appliquent **automatiquement** au démarrage, de façon
idempotente. Et j'ai vérifié ce déploiement en conditions réelles complètes, sur une base fraîche :
inscription, connexion, chat sourcé — de bout en bout, à travers toute la pile conteneurisée. »

> *Transition :* « Pour conclure, où en est le projet, et qu'est-ce qu'il reste à faire. »

---

### Slide 19 — Bilan & perspectives *(~55 s)*
« En résumé, ce qui est **livré et vérifié** : le socle métier avec son RBAC et son circuit qualité,
le pipeline RAG complet, l'orchestration avec ses garde-fous anti-hallucination, un frontend en
temps réel, et un déploiement Docker Compose fonctionnel de bout en bout.
`[clic]` Ce qu'il reste : améliorer la précision du Router — une nouvelle approche reste à évaluer ;
ajouter des endpoints de lecture pour des vues RH au-delà du simple chat ; traiter trois cas
particuliers métier encore ouverts, comme la mutation d'un collaborateur entre pôles ; et enfin,
connecter le profil vers le second serveur Ollama de l'entreprise, avec des modèles plus capables. »

> *Transition :* « Je vous remercie de votre attention. »

---

### Slide 20 — Merci / Questions *(~15 s)*
« **Merci de votre attention.** Je suis à votre disposition pour vos questions — et, si vous le
souhaitez, pour une démonstration en direct de l'application. »

---

## Antisèche — formules de secours pour les questions

- *« Pourquoi l'architecture hexagonale, concrètement ? »* → tester le métier sans base de données
  ni LLM réel ; remplacer une techno (SQL Server, Qdrant, Ollama) sans toucher au métier.
- *« Comment évitez-vous les hallucinations, précisément ? »* → double porte de sortie **en code**
  avant tout appel au Generator (0 candidat Qdrant, ou rien sous le seuil après reranking) — jamais
  une simple consigne de prompt.
- *« Le Router se trompe dans 27% des cas, n'est-ce pas grave ? »* → mesuré et documenté
  explicitement, pas caché. Le mode de défaillance reste « gracieusement faux » : une mauvaise
  catégorie, jamais une réponse inventée avec assurance — grâce au garde-fou du Generator, qui est
  indépendant du Router.
- *« Pourquoi Ollama en local plutôt qu'une API cloud ? »* → souveraineté des données RH,
  disponibilité hors-ligne, coût prévisible pendant le développement ; le modèle est configurable
  (nom + URL) pour basculer vers un serveur plus capable sans recompiler.
- *« Qui peut voir/modifier les données de qui ? »* → RBAC + `DepartmentScopeGuard`, vérifié **avant** le
  rôle. Un RH hors de son pôle est refusé, quel que soit son rôle par ailleurs.
- *« Le JWT est-il exposé au navigateur ? »* → jamais — pattern BFF, cookie httpOnly posé côté
  serveur uniquement, invisible même à un script XSS.
- *« Le système peut-il agir tout seul (écrire, supprimer) ? »* → jamais. Le Router/Generator ne
  passent que par des ports de lecture seule — décision de conception volontaire, pas une limite
  technique.
- *« Passage à plus grande échelle / production ? »* → remplacer les adaptateurs concernés
  (Infrastructure) sans toucher au métier ; Docker Compose reste adapté à une démo, pas à une
  production à grande échelle (pas d'orchestrateur type Kubernetes ici).
