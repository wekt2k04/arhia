import { ARHIA_API_URL } from "./config";
import { obtenirToken } from "./session";
import { ApiError } from "./errors";

export type ChecklistItem = {
  id: string;
  label: string;
  status: number;
  comment: string | null;
  checkedBy: string | null;
  checkedDate: string | null;
};

export type ChecklistSection = { name: string; order: number; items: ChecklistItem[] };

export type WorkflowInstanceDetail = {
  id: string;
  employeeId: string;
  employeeFullName: string;
  employeeNumber: string;
  departmentId: string;
  templateId: string;
  templateVersion: string;
  type: number;
  status: number;
  createdAt: string;
  closureDate: string | null;
  sections: ChecklistSection[];
};

export type WorkflowInstanceListItem = {
  id: string;
  employeeId: string;
  employeeFullName: string;
  employeeNumber: string;
  departmentId: string;
  type: number;
  status: number;
  createdAt: string;
  closureDate: string | null;
  totalItems: number;
  doneItems: number;
  failedItems: number;
  pendingItems: number;
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

export const getWorkflowInstance = (id: string) => appelApi<WorkflowInstanceDetail>(`/api/workflows/${id}`);
export const listWorkflows = (employeeId?: string) =>
  appelApi<WorkflowInstanceListItem[]>(`/api/workflows${employeeId ? `?employeeId=${employeeId}` : ""}`);
