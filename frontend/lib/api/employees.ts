import { ARHIA_API_URL } from "./config";
import { obtenirToken } from "./session";
import { ApiError } from "./errors";

export type Employee = {
  id: string;
  employeeNumber: string;
  lastName: string;
  firstName: string;
  jobTitle: string;
  departmentId: string;
  contractType: number;
  startDate: string;
  departureDate: string | null;
};

async function appelApi<T>(chemin: string): Promise<T> {
  const token = await obtenirToken();
  if (!token) throw new ApiError(401, "Non authentifié.");

  const reponse = await fetch(`${ARHIA_API_URL}${chemin}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!reponse.ok) {
    const texte = await reponse.text().catch(() => "");
    throw new ApiError(reponse.status, texte || `Échec de l'appel ${chemin}.`);
  }
  return reponse.json();
}

export const getEmployee = (id: string) => appelApi<Employee>(`/api/employees/${id}`);
export const listEmployees = () => appelApi<Employee[]>(`/api/employees`);
