"use client";

import { useRouter } from "next/navigation";
import { LogOut } from "lucide-react";
import { Button } from "@/components/ui/button";

export function BoutonDeconnexion() {
  const router = useRouter();

  async function seDeconnecter() {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push("/");
    router.refresh();
  }

  return (
    <Button variant="outline" size="sm" onClick={seDeconnecter}>
      <LogOut aria-hidden />
      Se déconnecter
    </Button>
  );
}
