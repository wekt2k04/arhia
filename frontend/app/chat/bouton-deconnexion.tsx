"use client";

import { useRouter } from "next/navigation";

export function BoutonDeconnexion() {
  const router = useRouter();

  async function seDeconnecter() {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push("/");
    router.refresh();
  }

  return (
    <button
      onClick={seDeconnecter}
      className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100"
    >
      Se déconnecter
    </button>
  );
}
