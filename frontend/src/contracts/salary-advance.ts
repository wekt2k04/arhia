/**
 * Contrats d'avance sur salaire AGIRH.
 * ---------------------------------------------------------------------------
 * Wire format : camelCase (défauts MVC .NET 8).
 * Sources backend :
 *   - src/Agirh.Api/Controllers/SalaryAdvanceController.cs
 *   - src/Agirh.Api/Dtos/SalaryAdvanceResponseDto.cs
 *   - src/Agirh.Domain/Entities/SalaryAdvanceRequest.cs (statuts autorisés)
 * ---------------------------------------------------------------------------
 */

export const SALARY_ADVANCE_STATUSES = ['Pending', 'Approved', 'Rejected'] as const;

export type SalaryAdvanceStatus = (typeof SALARY_ADVANCE_STATUSES)[number];

/**
 * GET /api/salary-advance/{id} → SalaryAdvanceResponseDto (wire camelCase).
 *
 * Vérifié sur src/Agirh.Api/Dtos/SalaryAdvanceResponseDto.cs :
 *   - `amountRequested` (decimal) — il n'y a PAS de champ `amount` ;
 *   - `requestDate` (DateTime, ISO 8601) — il n'y a PAS de champ `requestedAt` ;
 *   - `status` ∈ { Pending, Approved, Rejected } (voir
 *     src/Agirh.Domain/Entities/SalaryAdvanceRequest.cs) ;
 *   - PAS de `message`/`reason` dans la réponse GET : le motif de refus n'est
 *     JAMAIS exposé au client.
 */
export interface SalaryAdvance {
  id: string;
  amountRequested: number;
  status: SalaryAdvanceStatus;
  requestDate: string; // ISO 8601
  employeeId: string;
}
