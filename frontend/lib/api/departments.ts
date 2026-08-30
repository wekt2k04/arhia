import { ARHIA_API_URL } from "./config";
import { obtenirToken } from "./session";
import { ApiError } from "./errors";

export type Department = { id: string; name: string };

export async function listDepartments(): Promise<Department[]> {
  const token = await obtenirToken();
  if (!token) throw new ApiError(401, "Non authentifié.");

  const reponse = await fetch(`${ARHIA_API_URL}/api/departments`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!reponse.ok) {
    const texte = await reponse.text().catch(() => "");
    throw new ApiError(reponse.status, texte || "Échec de la récupération des pôles.");
  }
  return reponse.json();
}
