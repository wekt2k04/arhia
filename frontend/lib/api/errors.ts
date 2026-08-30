export class ApiError extends Error {
  constructor(public readonly status: number, message: string, public readonly code?: string) {
    super(message);
    this.name = "ApiError";
  }
}

export function estApiError(erreur: unknown): erreur is ApiError {
  return erreur instanceof ApiError;
}
