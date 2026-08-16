# Flux temps réel & workflows métier : comment l'information circule dans AGIRH

*Document autonome. Il couvre le mécanisme de communication en temps réel (SSE) et les trois
grands cycles métier du système : la conversation avec l'agent, la validation qualité d'un
template, et l'onboarding d'un collaborateur. Ces trois flux traversent toutes les couches
décrites dans les documents précédents (architecture, RAG, orchestration, Docker) — ce document
les recompose en parcours complets, du clic utilisateur à la réponse.*

## SSE : pourquoi un flux, et pas juste une réponse HTTP classique

Une requête HTTP classique fonctionne en tout-ou-rien : le client envoie une requête, attend, et
reçoit une réponse complète d'un coup. Ce modèle ne convient pas à deux besoins précis d'AGIRH :
afficher la réponse de l'agent conversationnel **au fur et à mesure** qu'elle est générée (comme
un humain qui tape au clavier), et tenir une **barre de notifications** informée en continu sans
que l'utilisateur ait à recharger la page.

Trois familles de solutions existent pour ce genre de besoin :

- **Le polling** — le client interroge répétitivement le serveur ("y a-t-il du nouveau ?") à
  intervalle régulier. Simple à implémenter, mais gaspille des requêtes inutiles quand rien n'a
  changé, et introduit un délai égal à l'intervalle de sondage.
- **Les WebSockets** — un canal bidirectionnel persistant, où client et serveur peuvent envoyer des
  messages à tout moment dans les deux sens. Puissant, mais plus complexe à mettre en œuvre et à
  faire passer à travers certaines infrastructures réseau (proxys, load balancers) que du HTTP
  classique.
- **SSE (Server-Sent Events)** — un flux HTTP **unidirectionnel**, du serveur vers le client
  uniquement, où le serveur peut envoyer des événements successifs sur une connexion ouverte, sans
  jamais la refermer entre deux événements. Le client s'appuie sur l'API navigateur standard
  `EventSource`, qui gère nativement la reconnexion automatique en cas de coupure.

AGIRH utilise SSE pour ses deux besoins, et **un seul mécanisme de transport pour les deux
usages** est un choix de simplicité délibéré — pas deux technologies différentes à maintenir. SSE
convient parce que dans les deux cas (chat, notifications), c'est bien le serveur qui pousse de
l'information vers le client ; le client n'a jamais besoin d'envoyer un message *sur la même
connexion* en retour (la question initiale du chat part en paramètre d'URL, avant l'ouverture du
flux SSE) — ce qui rend un WebSocket bidirectionnel superflu ici.

**Piège réel rencontré dans le développement d'AGIRH** : la compression HTTP intégrée de Next.js
**bufferise** toute la réponse avant de l'envoyer, un comportement pensé pour des réponses
classiques (compresser efficacement demande de voir tout le contenu d'abord), mais totalement
incompatible avec un flux progressif — rien n'arrivait au client avant la toute fin du flux côté
serveur, annulant l'intérêt même du streaming. Le correctif (`compress: false` dans
`next.config.ts`) illustre un piège général à connaître : une optimisation par défaut, pensée pour
le cas classique, peut silencieusement casser un cas d'usage différent (ici, le streaming) sans
lever d'erreur explicite — juste un flux qui semble muet puis déverse tout d'un coup.

## Trace complète n°1 : une question documentaire dans le chat

Ce parcours traverse la quasi-totalité des couches décrites dans les documents précédents :

1. Le navigateur ouvre un objet `EventSource` vers une route interne Next.js
   (`app/api/chat/demander`) — c'est le pattern BFF (document 01) : le navigateur ne parle jamais
   directement au backend .NET.
2. Cette route, exécutée côté serveur, relaie la requête vers `Agirh.Api`
   (`GET /api/chat/demander?question=...`, traité par `ChatController.Demander`), en y joignant le
   JWT récupéré du cookie httpOnly — jamais transmis au navigateur en clair.
3. Le use case correspondant appelle le **Router** (document 03), qui classe la question comme
   `DOCUMENTAIRE`.
4. Le pipeline RAG s'exécute (document 02) : embedding de la question, recherche Qdrant,
   reranking cross-encodeur.
5. **Garde-fou anti-hallucination** : si aucun chunk pertinent n'est retourné, le système répond
   directement "je n'ai pas trouvé cette information" — le **Generator n'est jamais appelé** dans
   ce cas, empêchant structurellement toute tentative d'invention de réponse.
6. Sinon, les chunks rerankés sont transmis au **Generator**, qui écrit la réponse. Chaque
   fragment de texte généré part immédiatement en frame SSE nommée `fragment`.
7. Une fois la génération terminée, une frame finale `termine` porte deux informations : si la
   réponse est effectivement sourcée (`sourcee`), et la liste des sources utilisées.
8. Côté navigateur, le composant `ChatWidget` accumule chaque fragment reçu dans le message en
   cours de construction (l'utilisateur voit le texte apparaître progressivement), puis affiche
   les sources une fois la frame `termine` reçue.

Ce parcours illustre concrètement comment les garde-fous décrits dans le document 03 (fail-safe,
anti-hallucination en code) s'insèrent physiquement dans le flux d'exécution réel, pas seulement
comme un principe abstrait.

## Trace complète n°2 : le circuit de validation d'un template

AGIRH ne permet jamais à un nouveau modèle de checklist (un "template") d'être utilisé directement
après sa création — un circuit de validation à plusieurs étapes est obligatoire, reflet direct
d'une exigence de conformité qualité (contexte SMSI réel du porteur du projet) où une modification
de procédure ne peut pas être appliquée sans révision indépendante.

1. Un RH propose une nouvelle version d'un template (`POST /api/templates`,
   `TemplateController.Proposer`) — statut initial : `Brouillon`.
2. Un rôle Admin/Qualité, agissant comme **Vérificateur**, valide une première fois
   (`POST /api/templates/{id}/verifier`) — le statut passe à vérifié/en validation.
3. Un rôle Admin/Qualité, agissant comme **Approbateur** (une étape distincte, même si porteur
   par la même population de rôle dans la pratique actuelle), approuve définitivement
   (`POST /api/templates/{id}/approuver`) — le statut final devient `Approuve`, et cette version
   est figée.
4. Une route de rejet existe symétriquement (`POST /api/templates/{id}/rejeter`), qui renvoie le
   template en amont dans le circuit plutôt que de l'approuver.
5. **Contrainte structurelle clé** : seul un template au statut `Approuve` peut servir à
   instancier un `WorkflowInstance` (voir trace n°3) — un onboarding réel ne peut jamais partir
   d'un brouillon non validé. Cette contrainte est vérifiée par le use case lui-même, pas
   seulement documentée : c'est une règle métier encodée, pas une convention qu'on espère
   respectée.
6. À chaque changement de statut, une notification SSE est envoyée vers le rôle suivant attendu
   dans le circuit (le même mécanisme SSE que le chat, réutilisé pour un usage différent).

Ce flux est un exemple concret de séparation des responsabilités appliquée à un processus humain,
pas seulement à du code : trois rôles distincts dans le circuit (Rédacteur, Vérificateur,
Approbateur) empêchent qu'une seule personne valide unilatéralement son propre travail — un
principe de contrôle interne classique, transposé ici en contrainte logicielle.

## Trace complète n°3 : l'onboarding d'un collaborateur

1. Un RH crée une fiche collaborateur (`POST /api/workflows`,
   `WorkflowController.Instancier`) avec les informations qui déterminent quel parcours
   s'applique : Poste, Pôle, Contrat, Date.
2. Le use case `ResoudreReferentielItems` calcule la liste précise des items de checklist attendus,
   en croisant ces trois dimensions (**Poste × Pôle × Contrat**) contre le template *approuvé*
   correspondant (contrainte héritée directement de la trace n°2).
3. Une `WorkflowInstance` et son ensemble de `ChecklistItem` sont persistés en base, statut
   initial `EnCours`.
4. Chaque item se coche indépendamment au fil du temps
   (`POST /api/workflows/{id}/items/{itemId}/cocher`) — chaque action est enregistrée dans
   l'**audit trail** (voir plus bas), pas seulement dans le log technique.
5. Le cycle de vie se termine par une clôture (`POST /api/workflows/{id}/cloturer`) puis, plus
   tard, un archivage (`POST /api/workflows/{id}/archiver`) qui rend le dossier définitivement
   consultable en lecture seule — un dossier archivé ne peut plus être modifié, garantissant
   l'intégrité de l'historique pour un usage de conformité.

## Deux flux de logs séparés : une distinction à ne pas confondre

AGIRH maintient délibérément **deux flux de journalisation distincts**, avec des objectifs
différents :

- **Le log technique** — erreurs, informations de debug, pensé pour un usage de développement :
  diagnostiquer une panne, comprendre un comportement inattendu du code.
- **L'audit trail** — une trace strictement **métier** : qui a coché quel item, qui a
  validé/rejeté quel template à quelle étape du circuit, qui a créé/clôturé/archivé quel dossier.

La distinction n'est pas cosmétique. Un log technique peut être verbeux, bruyant, voire
temporaire (rotation, purge) — il sert un usage opérationnel de court terme. Un audit trail, à
l'inverse, doit répondre de façon fiable et durable à la question "qui a fait quoi, et quand" pour
des raisons de conformité (ici, le contexte réel de qualité SMSI du porteur du projet l'exige
explicitement) — mélanger les deux flux risquerait soit de noyer l'information de conformité dans
du bruit technique, soit de gonfler artificiellement un audit trail avec des détails
d'implémentation qui n'ont pas leur place dans une preuve de conformité.

## Synthèse : ce que ces trois traces montrent ensemble

Les trois flux tracés dans ce document partagent une structure commune, révélatrice de la
philosophie de conception du système entier : une **action utilisateur déclenche un use case**
(couche Core, document 01), qui **traverse des ports vers des adaptateurs concrets**
(Infrastructure), avec des **vérifications de portée/rôle appliquées avant toute écriture**
(RBAC/PoleScopeGuard), et une **notification en temps réel** de l'événement qui en résulte (SSE)
quand un autre acteur du système doit en être informé. Comprendre un seul de ces flux en
profondeur — n'importe lequel des trois — donne une compréhension transférable des deux autres,
parce que la structure sous-jacente est la même à chaque fois.
