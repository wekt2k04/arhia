import type { SalaryAdvance } from '@/contracts';
import { apiFetch } from './browser';

/**
 * Service avance sur salaire — côté navigateur (client components uniquement).
 * ---------------------------------------------------------------------------
 * Wire format camelCase vérifié sur SalaryAdvanceResponseDto.cs :
 * `{ id, amountRequested, status, requestDate, employeeId }`.
 *
 * La réponse GET ne contient NI `message` NI `reason` : le motif de refus n'est
 * JAMAIS exposé au client (anti-fuite) — le widget n'a donc rien à afficher
 * d'autre que montant / statut / date.
 *
 * Le fetch same-origin passe par le proxy BFF générique
 * `src/app/api/[...path]/route.ts` qui injecte le Bearer depuis le cookie
 * httpOnly `agirh_token` (lib/api/browser.ts).
 * ---------------------------------------------------------------------------
 */

/**
 * GET /api/salary-advance/{id} → SalaryAdvanceResponseDto.
 *
 * Comportement backend (SalaryAdvanceController.cs) :
 *  - 401 → redirigé vers /login par apiFetch (session absente/expirée) ;
 *  - 403 (compte inactif) et 404 (inexistante OU invisible, anti-énumération)
 *    lèvent `ApiError` ; le widget les rend de façon INDISTINGUABLE.
 */
export async function getSalaryAdvance(id: string): Promise<SalaryAdvance> {
  return apiFetch<SalaryAdvance>(`/api/salary-advance/${encodeURIComponent(id)}`);
}
