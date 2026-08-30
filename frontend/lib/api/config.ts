// Base de l'Api .NET (Arhia.Api). Appelee uniquement depuis du code serveur (Route Handlers,
// jamais depuis un composant client) — c'est le coeur du pattern BFF (docs/STACK_TECHNIQUE.md #2) :
// le navigateur ne parle qu'a Next.js, jamais directement a l'Api.
export const ARHIA_API_URL = process.env.ARHIA_API_URL ?? "http://localhost:5080";
