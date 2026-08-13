/**
 * Contrats d'authentification AGIRH.
 * ---------------------------------------------------------------------------
 * Wire format : camelCase. Vérifié sur tests.http (projet) :
 *   `{{loginAdmin.response.body.token}}` et sur les défauts MVC .NET 8
 *   (JsonSerializerDefaults.Web — Program.cs n'ajoute aucun AddJsonOptions).
 * Source backend : src/Agirh.Api/Controllers/AuthController.cs
 * ---------------------------------------------------------------------------
 */

export type AgirhRole = 'Admin' | 'Manager' | 'Collaborator';

/** POST /api/auth/login — corps attendu (AuthController.Login). */
export interface LoginRequest {
  /** Email de l'employé (trim + lower-invariant côté backend). */
  email: string;
  /** Mot de passe brut. Le backend accepte null (échec garanti → 401). */
  password: string | null;
}

/** POST /api/auth/login — réponse 200 (objet anonyme du contrôleur). */
export interface LoginResponse {
  /** JWT — ne doit JAMAIS être exposé au client (cookie httpOnly, cf. BFF). */
  token: string;
  employeeId: string;
  role: AgirhRole;
  firstName: string;
  lastName: string;
}

/** Corps d'erreur standard du backend : `{ message: string }`. */
export interface ApiErrorBody {
  message: string;
}
