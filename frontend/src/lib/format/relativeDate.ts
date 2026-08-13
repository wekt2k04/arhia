/**
 * Formatage de date relative en français (présentation uniquement).
 * ---------------------------------------------------------------------------
 *  - < 1 min   → « à l'instant »
 *  - < 60 min  → « il y a X min »
 *  - < 24 h    → « il y a X h »
 *  - < 7 jours → « il y a X j »
 *  - sinon     → « 12 janv. » (même année) / « 12 janv. 2025 » (sinon)
 *
 * Tolérant : toute date invalide produit une chaîne vide (l'UI n'affiche rien).
 * ---------------------------------------------------------------------------
 */

const MINUTE_MS = 60_000;
const HOUR_MS = 3_600_000;
const DAY_MS = 86_400_000;

export function formatRelativeDate(isoDate: string): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) return '';

  const now = Date.now();
  const elapsedMs = now - date.getTime();

  const minutes = Math.floor(elapsedMs / MINUTE_MS);
  if (minutes < 1) return "à l'instant";
  if (minutes < 60) return `il y a ${minutes} min`;

  const hours = Math.floor(elapsedMs / HOUR_MS);
  if (hours < 24) return `il y a ${hours} h`;

  const days = Math.floor(elapsedMs / DAY_MS);
  if (days < 7) return `il y a ${days} j`;

  const options: Intl.DateTimeFormatOptions =
    date.getFullYear() === new Date(now).getFullYear()
      ? { day: 'numeric', month: 'short' }
      : { day: 'numeric', month: 'short', year: 'numeric' };

  return new Intl.DateTimeFormat('fr-FR', options).format(date);
}
