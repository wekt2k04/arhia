# Script orateur — AGIRH v3 (pyramide de Minto)

## 1 — AGIRH

Annoncer : déploiement d'un pipeline IA multi-agents, fiabilisé par un architecture Zero-Trust, au service des RH.

## 2 — Un pipeline IA multi-agents déployé de bout en bout

La réponse d'abord (pyramide de Minto) : le pipeline est en production, mesuré, testé. Ensuite on raconte comment.

## 3 — Trajectoire

Quatre actes : comprendre le risque, le neutraliser, l'exploiter, le prouver.

## 4 — Le Défi

Poser le problème : pourquoi un LLM nu ne suffit pas.

## 5 — Les LLM nus ne peuvent pas agir sur des données RH sensibles

Quatre frictions : fiabilité, confidentialité, cloisonnement, réactivité.

## 6 — Zero-Trust par défaut : aucun accès sans preuve, aucune erreur ouverte

Le cahier des charges de sécurité, non négociable.

## 7 — Le Bouclier

Montrer la structure qui encadre l'IA.

## 8 — Architecture hexagonale : domaine pur, découplé, testable

Domain → Core → Infrastructure → Api, oignon pur. Frontend découplé.

## 9 — Pipeline Actor-Critic : le RBAC (C#) ne dépend jamais du LLM

Profiler → Dispatcher(0 LLM) → Worker → Synthesizer → Checker. Séquencer à l'oral.

## 10 — Mitigation des failles TOCTOU par défaut fail-closed

Course check-then-act neutralisée : garde applicative + index unique filtré.

## 11 — La Machinerie

Plonger dans le code et les flux.

## 12 — Le dispatcher C# tranche avant tout appel au LLM

3 lignes vitales surlignées : Dispatch, DENIED, Checker. Le reste grisé.

## 13 — La réponse s'ancre sur la base RH, pas sur la mémoire du modèle

RAG = ancrage sur la connaissance RH ; échec LLM = dégradation, jamais de fuite.

## 14 — Cap métier à 50 % : 5 000 € accepté, 5 000,01 € refusé

Cas Stripe-like : un flux métier clair, un plafond exact, un seul état en attente.

## 15 — Le JWT ne quitte jamais le serveur : BFF en cookie httpOnly

Déposer capture_chat.png / capture_widget.png pour activer le mockup + spotlight.

## 16 — La Validation

Conclure par les résultats.

## 17 — Des performances mesurées, reproductibles, prouvées

Data storytelling : chiffres extra-larges, preuves visuelles.

## 18 — Industrialisation : de la démo au déploiement

La démo est validée ; l'industrialisation reste à livrer.

## 19 — Un assistant RH agentique, sécurisé, prêt à l'industrialisation

Synthèse : chaque livrable est un bloc prouvé.

## 20 — Merci

Ouverture aux questions.

## 21 — Annexe A — Modèle de données

Entités : Employee, LeaveRequest, SalaryAdvanceRequest, KnowledgeDocument.
