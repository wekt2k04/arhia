# Frontend & Deployment — AGIRH (V6)

> **MISE À JOUR V6 : le frontend Blazor WASM (`Agirh.Web`) est ABANDONNÉ.** Le nouveau frontend est **Next.js 15.5.22 (App Router) + TypeScript + TailwindCSS**, dans `frontend/`. Ce document décrit l'architecture actuelle.

## 1. Résumé exécutif (3 puces)

- **Next.js 15 App Router + Tailwind** : composants client (`'use client'`) pour le chat, BFF serveur pour l'API ; **First Load JS 103 kB** (chat 206 kB), mobile-first (`h-dvh`, drawer).
- **Sécurité** : JWT en **cookie httpOnly** (BFF), **sanitization stricte** du Markdown (0 sink XSS), CSP prod, erreurs 401/403/404/429 gracieuses.
- **UX IA** : streaming SSE par lots (≤ 10 re-rendus/s), auto-scroll smart, widgets `||WIDGET…||`, chips `||SUGGEST…||`, **bulle orange** sur `denied`.

---

## 1bis. Architecture Next.js

| Couche | Chemin | Rôle |
|---|---|---|
| App shell | `frontend/src/app/layout.tsx` | `<html lang="fr">`, globals, sans CSP bloquant en dev |
| Auth | `frontend/src/app/(auth)/login/page.tsx` | Formulaire sécurisé (Entrée, états 401/429/503) |
| Chat | `frontend/src/app/chat/page.tsx` → `<ChatShell/>` | Store + composants chat |
| BFF | `frontend/src/app/api/auth/{login,session,logout}/route.ts` | JWT → cookie httpOnly |
| BFF agent | `frontend/src/app/api/agent/[...path]/route.ts` | Proxy SSE streamant (`duplex:'half'`) |
| BFF générique | `frontend/src/app/api/[...path]/route.ts` | Conversations, salary-advance, leave… |
| Middleware | `frontend/src/app/middleware.ts` | Garde `/chat` (redirect `/login?returnUrl=…`) + `/api/*` privés |
| Composants | `frontend/src/components/` | `chat/` (ChatShell, MessageList, MessageBubble, ChatInput, TypingIndicator, AssistantContent, SuggestionChips), `layout/` (AppShell, Sidebar), `ui/SafeMarkdown.tsx` |
| Contrats | `frontend/src/contracts/` | DTOs typés (auth, conversation, salary-advance, sse) — wire camelCase |
| Logique | `frontend/src/lib/` | `api/` (backendProxy, browser/apiFetch, chat/SSE, conversations, messages, salaryAdvance), `auth/` (cookies, jwt), `chat/markers.ts`, `format/`, `state/ChatProvider.tsx` |

### Routes

| Route | Type | Auth | Rôle |
|---|---|---|---|
| `/` | page | Non | Redirect → `/chat` |
| `/login` | page (client) | Non | Connexion |
| `/chat` | page (serveur → `ChatShell`) | **Oui** (middleware) | Chat IA |
| `/api/auth/login|session|logout` | Route Handler | — | BFF JWT |
| `/api/agent/[...path]` | Route Handler | cookie | Proxy SSE chat |
| `/api/[...path]` | Route Handler | cookie | Proxy générique REST |

### Précédence App Router
`/api/auth/*` > `/api/agent/[...path]` > `/api/[...path]` (les routes spécifiques gagnent sur le catch-all — vérifié au build).

---

## 2. Authentification — JWT en cookie httpOnly (jamais dans le JS)

### Flux

```
Login.razor(équiv.) → POST /api/auth/login (BFF)
   → BFF appelle POST {AGIRH_API_URL}/api/auth/login
   ← { token, employeeId, role, firstName, lastName } (camelCase)
   → setAuthCookie(agirh_token, httpOnly, Secure(prod), SameSite=Lax, Max-Age=exp-now)
   → renvoie { user }  — LE TOKEN N'EST JAMAIS RENVOYÉ AU CLIENT
```

### `frontend/src/lib/auth/cookies.ts`

```ts
export const AUTH_COOKIE_NAME = 'agirh_token';
export function getAuthCookie(request: Request): string | null;   // lu par les BFF
export function setAuthCookie(response: Response, token: string, maxAgeSeconds?: number): void;
export function clearAuthCookie(response: Response): void;
```

- Cookie : `HttpOnly`, `Secure` (prod), `SameSite=Lax`, `Path=/`, `Max-Age` aligné sur `exp` du JWT (défaut 8 h = 28800 s).
- **`/api/auth/session`** (GET) : lit le cookie, décode la payload (base64url), **valide `exp`** (tolérance 30 s) → `{ user }` ou 401. Signature re-vérifiée par le backend.
- **`/api/auth/logout`** (POST) : purge le cookie → 204.
- **`middleware.ts`** : cookie absent sur `/chat` → redirect `/login?returnUrl=…` ; sur `/api/*` privés → 401 JSON. Les BFF gèrent leurs propres 401.

### Garanties
| Propriété | État |
|---|---|
| Token accessible par le JS | ❌ (cookie httpOnly uniquement) |
| localStorage / sessionStorage | **jamais utilisés** |
| Token dans le bundle | ❌ (`AGIRH_API_URL` est serveur, pas `NEXT_PUBLIC_`) |
| 401 pendant l'UI | `apiFetch` → `window.location.replace('/login?returnUrl=…')` avant toute erreur |
| 429 login | message distinct « Trop de tentatives de connexion, réessayez plus tard. » |

---

## 3. BFF — proxys (couche d'intégration)

### `frontend/src/lib/api/backendProxy.ts` — helper factorisé

```ts
export async function forwardToBackend(request: NextRequest, pathname: string): Promise<Response>
```

- Lit `getAuthCookie(request)` → injecte `Authorization: Bearer <token>`.
- Filtre les en-têtes hop-by-hop + `host` ; `redirect: 'manual'`.
- Corps : pour les méthodes avec body, passe le `ReadableStream` brut avec `duplex: 'half'` (requis par fetch Node pour le SSE).
- Réponse : copie statut + en-têtes ; si `text/event-stream` → force `Content-Type: text/event-stream`, `Cache-Control: no-cache, no-transform`, `X-Accel-Buffering: no` — **jamais de buffering**.

Consommé par `api/agent/[...path]` et `api/[...path]`.

### Client navigateur — `lib/api/browser.ts`

`apiFetch<T>(path, init)` : fetch même-origine (cookie auto) ; `401` → redirect ; `!ok` → `ApiError` avec **message français générique** (jamais `ex.message`, jamais le corps backend brut).

---

## 4. Chat — streaming SSE (`lib/api/chat.ts`)

```ts
streamChat({ message, conversationId?, previousMessages, signal?, onConversation?,
             onTokens?(chunk: string[]), onDenied?(message), onError?(friendly) })
  : Promise<StreamResult>
```

- Payload : `{ message, conversationId, previousMessages }` — **`previousMessages ≤ 20` TOUJOURS envoyé** (= mémoire multi-tours).
- Lecture : `fetch` → `ReadableStream` → `TextDecoder` → frames `data: {json}\n\n`.
- **Flush par lots** : `onTokens` reçoit des lots (5 tokens OU ~100 ms) → ≤ ~10 re-rendus/s (vs ~33 en Blazor).
- Événements : `conversation` (id, refresh sidebar), `token`, **`denied`** (bulle orange), `error`, `done`.
- Erreurs : 401 → redirect ; 429 → « Trop de requêtes… » ; 5xx → générique FR ; 403 corps SSE parsé en défensif.

### Store — `lib/state/ChatProvider.tsx`

Context + `useReducer` (0 dépendance) : `{ messages, conversationId, isStreaming, isLoadingHistory, error, streamingAssistantId }`.
- Actions pures (send_start, append_tokens, stream_end, deny_message, stream_error, set_conversation, load_*, new_conversation).
- Effets de bord dans le provider : garde double-envoi, finalisation idempotente (`ended`), **annulation jamais transformée en erreur**, abort à la bascule de conversation, garde anti-course sur le chargement.

### Composants

| Composant | Rôle |
|---|---|
| `MessageList` | zone scrollable, **auto-scroll smart** (`scrollHeight - scrollTop - clientHeight < 80 px`, désactivé si l'utilisateur remonte), bouton « ↓ Revenir en bas », skeleton |
| `MessageBubble` | user droite / assistant gauche ; streaming = brut `whitespace-pre-wrap` ; finalisé = Markdown ; `denied` = **bulle orange** + icône + `role="alert"` ; erreur = gris discret |
| `ChatInput` | textarea auto-extensible, **Entrée = envoyer, Maj+Entrée = retour ligne**, bouton « Arrêter », focus auto |
| `TypingIndicator` | 3 points animés |
| `AssistantContent` | `parseMarkers(content)` → blocs text (SafeMarkdown) + widgets (SalaryAdvanceCard) + chips (SuggestionChips) |
| `SuggestionChips` | pills cliquables → `sendMessage` |

---

## 5. Markdown & Sécurité XSS (`ui/SafeMarkdown.tsx` + `lib/markdown/sanitizeSchema.ts`)

```
react-markdown → remark-gfm → rehype-sanitize(sanitizeSchema) → rehype-highlight
```

- `sanitizeSchema` : **liste blanche stricte** (21 tags), `attributes` `'*': []` (seuls `className` sur code/span et `href` sur `a`), `protocols.href = http/https/mailto`, `strip` = script/style/iframe/object/embed/link/meta/form/input/textarea/button/select/svg/math/video/audio/img, `allowComments:false`.
- Liens → `target="_blank" rel="noopener noreferrer nofollow"`.
- Blocs de code → **coloration** (highlight.js, thème github) + **bouton « Copier »** (micro-animation).
- **ZÉRO `dangerouslySetInnerHTML`** dans `frontend/src` (grep : 0 sink).
- Streaming : texte brut (pas de Markdown partiel) → finalisation : Markdown.

---

## 6. Marqueurs IA — `lib/chat/markers.ts`

```ts
export const UUID_REGEX = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
WIDGET_MARKER_REGEX = /\|\|WIDGET:SalaryAdvance:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\|\|/g
SUGGEST_MARKER_REGEX  = /\|\|SUGGEST:([^|]+)\|\|/g
parseMarkers(content): MarkerBlock[]       // text | widget | suggest — tokeniseur déterministe
stripPartialMarkers(raw): string            // anti-flash pendant le stream (store intact)
stripControlSentinel(content): string       // retire \u001f / DENIED (défense en profondeur)
```

- GUID **strict** 8-4-4-4-12 + revalidation (pas de `{36}` relâché) ; GUID invalide → retiré sans résidu.
- Widget rendu : `components/chat/widgets/SalaryAdvanceCard.tsx` — skeleton, erreur **unique** « Demande introuvable ou inaccessible. » (403 = 404 = anti-énumération), badge statut (Pending ambre / Approved vert / Rejected rouge), montant `fr-FR EUR`.
- Chips SUGGEST → envoi du texte comme message utilisateur.

---

## 7. Déploiement — Build & headers

### Dev

```bash
cd frontend
npm install            # Next 15.5.22, react 19, tailwindcss 3.4, TS 5.7
npm run dev            # http://localhost:3000
```

### Prod

```bash
npm run build          # 0 erreur (routes : /, /login, /chat, /api/auth/*, /api/agent/*, /api/[...path])
npm start              # http://localhost:3000
```

### Headers de sécurité (`next.config.mjs`, prod uniquement)

`Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'` + `X-Content-Type-Options: nosniff` + `Referrer-Policy: strict-origin-when-cross-origin` + `X-Frame-Options: DENY` + `Permissions-Policy`.

⚠️ **Réserve connue** : `script-src 'self'` sans nonce peut bloquer les scripts inline RSC en prod → stratégie **nonce via middleware** à trancher après smoke test `next start`.

### Taille

| Métrique | Valeur |
|---|---|
| First Load JS (global) | **103 kB** (vs ~9,6 MB gz Blazor) |
| `/chat` (avec react-markdown + highlight) | **206 kB** |
| Build | ~10 s (types + lint inclus) |

---

## 8. Variables d'environnement

```env
# frontend/.env.local  (jamais NEXT_PUBLIC_ — serveur uniquement, gitignoré)
AGIRH_API_URL=http://localhost:5000
```

Le navigateur ne parle **qu'à son même-origine** (`/api/*` BFF) → `connect-src 'self'` valide, aucun CORS déclenché.

---

## 9. Config Docker (état actuel)

Le service web est lancé en **mode natif** (`npm run dev` en développement ; `npm run build && npm start` en production). La conteneurisation du frontend (Dockerfile Node multi-stage) a été **abandonnée** : la configuration actuelle ne conteneurise que SQL Server (`docker/sql/Dockerfile`).

---

## 10. Dépannage frontend

| Symptôme | Cause probable | Action |
|---|---|---|
| Redirection `/login` après login | Cookie non posé / session expirée | Vérifier `/api/auth/session` ; re-login |
| 401 sur `/api/[...path]` | Cookie absent côté BFF | Re-login ; vérifier middleware/BFF |
| Marqueurs `||WIDGET…||` visibles | Version obsolète | `npm run dev` ; vérifier `markers.ts` |
| Bulle orange absente sur refus | Backend pas relancé depuis Phase 6 | Relancer `dotnet run` |
| CSP casse l'hydratation (prod) | `script-src 'self'` sans nonce | Smoke test `next start` ; stratégie nonce |
| `npm run build` lent | Node 18 (Next 15) | Passer Node ≥ 20 (Next 16) |
| Sidebar non escamotable | Vue ≥ 768 px (desktop) | Tester < 768 px (mobile) |

---

## 11. Tableau de bord de suivi

| It. | Livrable | Statut |
|---|---|---|
| 0 | `00` + `07` | ✅ |
| 1 | `01_CORE_ARCHITECTURE` | ✅ |
| 2 | `02_SECURITY_AND_RBAC` | ✅ |
| 3 | `03_AI_AND_RAG_PIPELINE` | ✅ |
| 4 | `04_QA_AND_TESTING` + `05` (polish) | ✅ |
| 5 | `06_Docker_Orchestration` | ✅ |
