/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  MARQUEURS IA — parsing STRICT des marqueurs pipeline Phase 5.
 * ─────────────────────────────────────────────────────────────────────────────
 *  Contrat backend (AgentOrchestratorService.cs l. 166-194 + CreateSalaryAdvanceRequest.cs) :
 *    - `||WIDGET:SalaryAdvance:{guid}||` — carte adaptative (avance créée) ;
 *    - `||SUGGEST:Texte||` — quick reply (intention LeaveBalance / SalaryAdvance).
 *  L'orchestrateur émet AU PLUS UN marqueur de chaque type, EN FIN de réponse,
 *  APRÈS le post-checker. Ce module ne les GÉNÈRE pas : il les PARSE.
 *
 *  Module PUR (zéro dépendance, zéro DOM) — testable de façon déterministe.
 *
 *  RÈGLES DE SÉCURITÉ DU RENDU :
 *    - Un token `||WIDGET:…||` NON strictement valide (GUID hors format canonique)
 *      est TOUT DE MÊME retiré du texte : jamais affiché brut.
 *    - Un token `||SUGGEST:…||` mal formé (vide, contient `|`) est retiré du
 *      texte : jamais affiché brut, jamais émis en bloc.
 *    - Un fragment non fermé `||WIDGET:…` / `||SUGGEST:…` (anti-flash pendant le
 *      streaming) est retiré par `stripPartialMarkers`.
 *    - Aucune trace de marqueur ne subsiste dans le rendu.
 * ---------------------------------------------------------------------------
 */

/** GUID au format canonique exact (RFC 4122 / format `Guid.ToString()` .NET). */
export const UUID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

/**
 * Marqueur WIDGET STRICT : GUID à la position EXACTE du format canonique
 * (8-4-4-4-12, hexadécimal) — PAS de `{36}` relâché (le backend émet
 * `Guid.NewGuid().ToString()`, toujours canonique ; la capture est revalidée
 * par UUID_REGEX).
 */
export const WIDGET_MARKER_REGEX =
  /\|\|WIDGET:SalaryAdvance:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\|\|/g;

/** Marqueur SUGGEST : texte sans `|` (un `|` termine le token, jamais interpolé). */
export const SUGGEST_MARKER_REGEX = /\|\|SUGGEST:([^|]+)\|\|/g;

/** Bloc d'une réponse assistant FINALISÉE, dans l'ordre du contenu. */
export type MarkerBlock =
  | { kind: 'text'; content: string }
  | { kind: 'widget'; id: string }
  | { kind: 'suggest'; text: string };

/** Versions ANCRÉES (token complet) pour valider un candidat extrait. */
const WIDGET_TOKEN_ANCHORED =
  /^\|\|WIDGET:SalaryAdvance:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\|\|$/;
const SUGGEST_TOKEN_ANCHORED = /^\|\|SUGGEST:([^|]+)\|\|$/;

/**
 * Défense en profondeur — sentinelle de contrôle backend.
 * ---------------------------------------------------------------------------
 * L'événement SSE `denied` (et, par prudence, tout contenu assistant) peut
 * transporter le marqueur de contrôle `\u001f` (Unit Separator, U+001F) et/ou
 * le préfixe littéral `DENIED` posés par l'orchestrateur avant filtrage
 * serveur. Si ce sentinel fuit malgré le nettoyage côté backend, l'UI ne
 * doit JAMAIS l'afficher.
 *
 * Règles (conservatrices — aucune regex WIDGET/SUGGEST modifiée ici) :
 *  - tout caractère `\u001f` est retiré, où qu'il se trouve ;
 *  - un préfixe littéral `DENIED` (émission backend, majuscules exactes)
 *    est retiré s'il est suivi d'un séparateur (`:`, `-`, `_`, espace…)
 *    ou de la fin de chaîne — `DENIEDmessage` (sans séparateur) n'est PAS
 *    un sentinel, on ne mutile pas le texte ;
 *  - le résultat est trimé (pas d'espace résiduel après retrait).
 */
export function stripControlSentinel(content: string): string {
  if (content.length === 0) return content;

  // 1) Sentinelle de contrôle U+001F — globalement (peut apparaître
  //    n'importe où si le backend concatène avant le filtre).
  let cleaned = content.replace(/\u001f/g, '');

  // 2) Le sentinel est un PRÉFIXE : on aligne sur le début de chaîne
  //    (un espace / retour à la ligne parasite ne doit pas le masquer).
  cleaned = cleaned.trim();

  // 3) Préfixe littéral `DENIED` + séparateur éventuel.
  cleaned = cleaned.replace(/^DENIED(?:[\s:._-]+|$)/, '');

  return cleaned.trim();
}

/** Préfixes qui font d'un `||…||` un CANDIDAT marqueur (strict validé ensuite). */
function isMarkerPrefix(inner: string): boolean {
  return inner.startsWith('WIDGET:') || inner.startsWith('SUGGEST:');
}

/**
 * Évalue un token `||…||` extrait (délimiteurs inclus).
 *  - WIDGET strict (GUID canonique, revalidé UUID_REGEX) → bloc `widget` ;
 *  - SUGGEST strict (`[^|]+`) → bloc `suggest` (texte trimé) ;
 *  - tout autre candidat (GUID invalide, suggestion vide/avec `|`) → `null`
 *    (le token est retiré du texte, JAMAIS affiché brut).
 */
function parseMarkerToken(token: string): MarkerBlock | null {
  const widgetMatch = WIDGET_TOKEN_ANCHORED.exec(token);
  if (widgetMatch) {
    const id = widgetMatch[1];
    if (UUID_REGEX.test(id)) {
      return { kind: 'widget', id };
    }
    return null;
  }

  const suggestMatch = SUGGEST_TOKEN_ANCHORED.exec(token);
  if (suggestMatch) {
    return { kind: 'suggest', text: suggestMatch[1].trim() };
  }

  return null;
}

/**
 * Ajoute un bloc `text` en FUSIONNANT avec le bloc text précédent s'il existe.
 * Nécessaire : un marqueur invalide retiré entre deux segments ne doit pas
 * produire deux paragraphes (ex. `a ||WIDGET:bad|| b` → `a  b`, UN seul bloc).
 */
function pushTextBlock(blocks: MarkerBlock[], content: string): void {
  if (content.length === 0) return;
  const last = blocks[blocks.length - 1];
  if (last && last.kind === 'text') {
    last.content += content;
  } else {
    blocks.push({ kind: 'text', content });
  }
}

/**
 * Découpe une réponse assistant FINALISÉE en blocs ORDONNÉS.
 *
 * Stratégie déterministe (tokeniseur par paires `||…||`) :
 *  - un candidat marqueur (`WIDGET:` / `SUGGEST:` juste après `||`) est évalué
 *    par les regex STRICTES ; s'il est valide → bloc dédié, sinon → retiré ;
 *  - un fragment non fermé `||WIDGET:…` / `||SUGGEST:…` (marqueur orphelin)
 *    est retiré, lui aussi (réponse finalisée après un arrêt utilisateur) ;
 *  - tout autre `||…||` n'est PAS un marqueur → conservé tel quel dans le texte ;
 *  - les blocs `text` adjacents sont fusionnés ; ils peuvent être vides
 *    (ignorés au rendu).
 */
export function parseMarkers(content: string): MarkerBlock[] {
  // Défense en profondeur : la sentinelle de contrôle backend (U+001F,
  // préfixe `DENIED`) est retirée AVANT le parsing des blocs — l'UI ne
  // l'affiche jamais, même si elle fuit le filtrage serveur.
  const clean = stripControlSentinel(content);
  if (clean.length === 0) return [];

  const blocks: MarkerBlock[] = [];
  let cursor = 0;
  let searchFrom = 0;
  let index = clean.indexOf('||', searchFrom);

  while (index !== -1) {
    const closeIndex = clean.indexOf('||', index + 2);

    // Fragment non fermé en fin de chaîne : marqueur orphelin → retiré.
    if (closeIndex === -1) {
      const inner = clean.slice(index + 2);
      if (isMarkerPrefix(inner)) {
        pushTextBlock(blocks, clean.slice(cursor, index));
        cursor = clean.length;
      }
      break;
    }

    const inner = clean.slice(index + 2, closeIndex);
    if (isMarkerPrefix(inner)) {
      pushTextBlock(blocks, clean.slice(cursor, index));
      const token = clean.slice(index, closeIndex + 2);
      const parsed = parseMarkerToken(token);
      if (parsed) blocks.push(parsed);
      cursor = closeIndex + 2;
      searchFrom = cursor;
      index = clean.indexOf('||', searchFrom);
    } else {
      // `||` non-marqueur : on continue la recherche après cette paire.
      searchFrom = index + 2;
      index = clean.indexOf('||', searchFrom);
    }
  }

  pushTextBlock(blocks, clean.slice(cursor));

  return blocks;
}

/**
 * Anti-flash pendant le STREAMING : retire tout marqueur du texte brut.
 *  - marqueurs COMPLETS (valides ou non) → retirés ;
 *  - fragments incomplets `||WIDGET:…` / `||SUGGEST:…` sans le `||` terminal →
 *    retirés (le backend découpe `||SUGGEST:Poser un congé||` en plusieurs
 *    tokens espacés : sans ce traitement, le fragment clignoterait à l'écran).
 *  - tout `||…||` non-marqueur → conservé.
 *
 * ⚠️ Usage UNIQUEMENT pour le rendu brut pendant le flux. Le contenu brut
 * stocké dans le store (ChatProvider) reste INTACT : `parseMarkers` opérera
 * dessus à la finalisation.
 */
export function stripPartialMarkers(raw: string): string {
  // Défense en profondeur : la sentinelle de contrôle backend (U+001F,
  // préfixe `DENIED`) est retirée AVANT le stripping des marqueurs — jamais
  // affichée à l'écran, même si elle fuit le filtrage serveur.
  const content = stripControlSentinel(raw);
  if (content.length === 0) return content;

  let result = '';
  let cursor = 0;
  let searchFrom = 0;
  let index = content.indexOf('||', searchFrom);

  while (index !== -1) {
    const closeIndex = content.indexOf('||', index + 2);

    // Fragment non fermé : tout ce qui suit le `||` du fragment est retiré
    // (le backend place TOUJOURS les marqueurs en fin de réponse).
    if (closeIndex === -1) {
      const inner = content.slice(index + 2);
      if (isMarkerPrefix(inner)) {
        result += content.slice(cursor, index);
        return result;
      }
      break;
    }

    const inner = content.slice(index + 2, closeIndex);
    if (isMarkerPrefix(inner)) {
      result += content.slice(cursor, index);
      cursor = closeIndex + 2;
      searchFrom = cursor;
      index = content.indexOf('||', searchFrom);
    } else {
      searchFrom = index + 2;
      index = content.indexOf('||', searchFrom);
    }
  }

  result += content.slice(cursor);
  return result;
}
