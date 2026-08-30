import { cookies } from "next/headers";

// Le JWT n'est jamais exposé au JavaScript client (docs/STACK_TECHNIQUE.md #2 : cookie httpOnly).
// Durée alignée sur Jwt:TokenLifetimeMinutes côté Arhia.Api (60 min par défaut,
// appsettings.json) — si l'un change, aligner l'autre.
const NOM_COOKIE = "arhia_session";
const DUREE_COOKIE_SECONDES = 60 * 60;

export async function definirSession(token: string): Promise<void> {
  const store = await cookies();
  store.set(NOM_COOKIE, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: DUREE_COOKIE_SECONDES,
  });
}

export async function obtenirToken(): Promise<string | null> {
  const store = await cookies();
  return store.get(NOM_COOKIE)?.value ?? null;
}

export async function effacerSession(): Promise<void> {
  const store = await cookies();
  store.delete(NOM_COOKIE);
}
