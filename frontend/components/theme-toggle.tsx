"use client";

import { useEffect, useState } from "react";
import { Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { Button } from "@/components/ui/button";

// resolvedTheme n'est connu qu'après montage côté client (next-themes lit localStorage/le
// système) — un rendu SSR devinerait à tort "light", d'où ce garde-fou anti-hydratation.
export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const [monte, setMonte] = useState(false);

  useEffect(() => setMonte(true), []);

  if (!monte) {
    return <Button variant="ghost" size="icon" disabled aria-hidden className="opacity-0" />;
  }

  const estSombre = resolvedTheme === "dark";

  return (
    <Button
      variant="ghost"
      size="icon"
      aria-label={estSombre ? "Passer en mode clair" : "Passer en mode sombre"}
      onClick={() => setTheme(estSombre ? "light" : "dark")}
    >
      {estSombre ? <Sun aria-hidden /> : <Moon aria-hidden />}
    </Button>
  );
}
