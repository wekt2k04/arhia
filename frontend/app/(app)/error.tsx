"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";
import { QueryState } from "@/components/states/query-state";

export default function ErreurApp({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <QueryState
        kind="error"
        message="Une erreur inattendue est survenue."
        action={
          <Button size="sm" variant="outline" onClick={reset}>
            Réessayer
          </Button>
        }
      />
    </div>
  );
}
