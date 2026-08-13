/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  Schéma de sanitisation STRICTE du Markdown LLM (contrat XSS — Phase 4).
 *
 *  Consommateur : `SafeMarkdown.tsx` (importe `sanitizeSchema` et le passe à
 *  `rehype-sanitize`). Politique : LISTE BLANCHE — rien n'est autorisé par
 *  défaut ; chaque élément/attribut/protocole doit être explicitement listé
 *  ci-dessous.
 *
 *  Sémantique d'exécution (`hast-util-sanitize` v5, vérifiée sur le source) :
 *      state.schema = {...defaultSchema, ...options}   // fusion SHALLOWE
 *  → Chaque clé fournie ici REMPLACE intégralement la valeur de
 *    `defaultSchema`. Le `defaultSchema` GitHub n'est donc JAMAIS utilisé tel
 *    quel : `tagNames` restreint (h1-h4, pas d'img, pas de input...),
 *    `attributes` réduit à 3 cas (`'*': []` écrase la liste permissive
 *    globale qui autorisait id/name/title/value/action...), `protocols`
 *    restreint (plus d'irc/xmpp), `strip` étendu à 15 balises dangereuses.
 *
 *  Clés NON surchargées → valeurs par défaut de `defaultSchema`, toutes
 *  sûres :
 *    - `ancestors` : `td/th/tr/tbody/thead/tfoot` exigent un ancêtre
 *      `table` (structure GFM valide uniquement) ;
 *    - `required` : ne cible que `input` (déjà INTERDIT + strip) → inerte ;
 *    - `allowComments: false`, `allowDoctypes: false` (verrouillées
 *      explicitement ci-dessous).
 *
 *  Note `clobber: []` : la protection anti-DOM-clobbering est désactivée
 *  comme spécifié, SANS risque — `id`/`name`/`aria*` ne sont pas dans
 *  `attributes`, ils ne peuvent donc jamais atteindre le DOM. `clobberPrefix`
 *  est conservé à sa valeur par défaut (inchangé).
 * ─────────────────────────────────────────────────────────────────────────────
 */

import type { Options } from 'rehype-sanitize';

/**
 * Politique stricte de rendu Markdown LLM.
 *
 * INJECTION EXÉCUTABLE = 0 : aucun `script`, `iframe`, `object`, `svg`,
 * `math`, `video`, `audio`, `img` ; aucun attribut `on*`, `style`, `id`,
 * `class` hors whitelist ; aucun protocole `javascript:` / `data:` /
 * `vbscript:` sur `href`.
 */
export const sanitizeSchema: Options = {
  /** Liste blanche d'éléments. Tout tag absent est supprimé (ou déballé s'il
   *  n'est pas dans `strip`). */
  tagNames: [
    // Structure / texte
    'p', 'br', 'hr',
    'strong', 'em', 'b', 'i', 'del', 'u',
    // Listes
    'ul', 'ol', 'li',
    // Titres
    'h1', 'h2', 'h3', 'h4',
    // Blocs / code
    'blockquote', 'code', 'pre', 'span',
    // Tableaux (GFM)
    'table', 'thead', 'tbody', 'tr', 'th', 'td',
    // Liens / conteneur
    'a', 'div',
  ],

  /**
   * Attributs autorisés PAR ÉLÉMENT (noms de propriétés HAST).
   *  - `'*': []` : AUCUN attribut par défaut — écrase la liste globale
   *    permissive du `defaultSchema` (id, name, title, value, action...).
   *  - `code`/`span` : `className` seul — classes hljs / `language-*` de
   *    coloration syntaxique. Une classe ne peut pas exécuter de script et
   *    aucun CSS hostile ne peut la cibler (style-src 'self' + <style>
   *    strippé + `style=` interdit).
   *  - `a` : `href` seul — `target`, `rel`, `title`, `id`... interdits.
   */
  attributes: {
    '*': [],
    code: ['className'],
    span: ['className'],
    a: ['href'],
  },

  /** Protocoles autorisés pour `href`. Les URL relatives (mêmes-origine) sont
   *  toujours acceptées par `hast-util-sanitize` (sans `:` de protocole).
   *  `javascript:`, `data:`, `vbscript:` (et variantes case-mixées) sont
   *  rejetés — la liste est en minuscules, le check est case-sensitive. */
  protocols: {
    href: ['http', 'https', 'mailto'],
  },

  /** Anti-DOM-clobbering désactivé — sans risque : `id`/`name`/`aria*` ne
   *  sont pas dans `attributes`, aucune propriété clobberable n'atteint le
   *  DOM. (`clobberPrefix` reste à la valeur par défaut, inchangée.) */
  clobber: [],
  clobberPrefix: 'user-content-',

  /** Balises à SUPPRIMER AVEC leur contenu (jamais déballées). Les éléments
   *  hors `tagNames` absents de `strip` sont, eux, déballés (children
   *  conservés) — comportement par défaut souhaité. */
  strip: [
    'script', 'style', 'iframe', 'object', 'embed',
    'link', 'meta',
    'form', 'input', 'textarea', 'button',
    'select',
    'svg', 'math', 'video', 'audio', 'img',
  ],

  /** Commentaires et doctypes : jamais. (Valeurs par défaut verrouillées
   *  explicitement — posture documentée.) */
  allowComments: false,
  allowDoctypes: false,
};
