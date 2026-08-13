'use client';

import { useEffect, useState } from 'react';

import type { SalaryAdvance, SalaryAdvanceStatus } from '@/contracts';
import { getSalaryAdvance } from '@/lib/api/salaryAdvance';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; advance: SalaryAdvance };

interface SalaryAdvanceCardProps {
  /** Id (GUID) de la demande — extrait du marqueur `||WIDGET:SalaryAdvance:{id}||`. */
  id: string;
}

/**
 * Carte adaptative d'une avance sur salaire (marqueur WIDGET).
 * ---------------------------------------------------------------------------
 *  - Fetch au montage via `getSalaryAdvance` (proxy BFF, cookie httpOnly).
 *  - États : skeleton shimmer (principe 32) / erreur générique UNIQUE avec
 *    « Réessayer » / données (montant fr-FR EUR, badge de statut coloré, date).
 *  - Anti-énumération : 403 (compte inactif) et 404 (inexistante ou invisible)
 *    rendent le MÊME message « Demande introuvable ou inaccessible. » — aucun
 *    détail technique, aucune distinction entre les deux.
 *  - Le champ `message`/motif de refus backend n'est PAS dans la réponse DTO ;
 *    le widget ne l'affiche donc jamais.
 *  - Zéro `dangerouslySetInnerHTML` : tout le contenu est rendu en texte React
 *    (échappé par défaut).
 * ---------------------------------------------------------------------------
 */
export default function SalaryAdvanceCard({ id }: SalaryAdvanceCardProps) {
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setState({ kind: 'loading' });

    getSalaryAdvance(id)
      .then((advance) => {
        if (!cancelled) setState({ kind: 'ready', advance });
      })
      .catch(() => {
        // 403/404/5xx/réseau : même message générique, jamais de détail.
        if (!cancelled) setState({ kind: 'error' });
      });

    return () => {
      cancelled = true;
    };
  }, [id, attempt]);

  if (state.kind === 'loading') return <LoadingSkeleton />;
  if (state.kind === 'error') {
    return <ErrorState onRetry={() => setAttempt((count) => count + 1)} />;
  }
  return <AdvanceCard advance={state.advance} />;
}

/* ─────────────────────────── États ─────────────────────────── */

/** Skeleton de chargement — même gabarit que la carte pour éviter le saut. */
function LoadingSkeleton() {
  return (
    <div
      className="max-w-sm rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
      role="status"
      aria-busy="true"
    >
      <span className="sr-only">Chargement de la demande d’avance…</span>
      <div className="flex items-center justify-between gap-3">
        <div className="h-4 w-28 animate-pulse rounded bg-slate-200/80" />
        <div className="h-5 w-20 animate-pulse rounded-full bg-slate-200/80" />
      </div>
      <div className="mt-3 h-7 w-36 animate-pulse rounded bg-slate-200/80" />
      <div className="mt-4 h-4 w-40 animate-pulse rounded bg-slate-200/80" />
    </div>
  );
}

/** Message d'erreur UNIQUE pour 403/404 (anti-énumération) + « Réessayer ». */
const ERROR_MESSAGE = 'Demande introuvable ou inaccessible.';

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="max-w-sm rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <p className="text-sm text-slate-600">{ERROR_MESSAGE}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-2.5 rounded-md border border-slate-300 bg-white px-2.5 py-1 text-xs font-medium text-slate-600 transition hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
      >
        Réessayer
      </button>
    </div>
  );
}

/* ─────────────────────── Carte données ─────────────────────── */

const STATUS_META: Record<SalaryAdvanceStatus, { label: string; badgeClass: string }> = {
  Pending: {
    label: 'En attente',
    badgeClass: 'bg-amber-50 text-amber-700 ring-1 ring-inset ring-amber-200',
  },
  Approved: {
    label: 'Approuvée',
    badgeClass: 'bg-emerald-50 text-emerald-700 ring-1 ring-inset ring-emerald-200',
  },
  Rejected: {
    label: 'Refusée',
    badgeClass: 'bg-red-50 text-red-700 ring-1 ring-inset ring-red-200',
  },
};

const AMOUNT_FORMATTER = new Intl.NumberFormat('fr-FR', {
  style: 'currency',
  currency: 'EUR',
});

function formatAmount(value: number): string {
  return AMOUNT_FORMATTER.format(value);
}

/** Date ISO → « 2 août 2026 » (fr). Tolérante : date invalide → chaîne vide. */
function formatRequestDate(isoDate: string): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) return '';
  return new Intl.DateTimeFormat('fr-FR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(date);
}

function AdvanceCard({ advance }: { advance: SalaryAdvance }) {
  // Défensif : le domaine clampe déjà les statuts inconnus vers « Pending »,
  // ce fallback ne devrait jamais être atteint.
  const statusMeta = STATUS_META[advance.status] ?? {
    label: advance.status,
    badgeClass: 'bg-slate-50 text-slate-700 ring-1 ring-inset ring-slate-200',
  };

  return (
    <div className="max-w-sm rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm font-medium text-slate-600">Avance sur salaire</p>
        <span
          className={[
            'shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium',
            statusMeta.badgeClass,
          ].join(' ')}
        >
          {statusMeta.label}
        </span>
      </div>

      <p className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">
        {formatAmount(advance.amountRequested)}
      </p>

      <div className="mt-3 flex items-center justify-between gap-3 border-t border-slate-100 pt-3 text-sm">
        <span className="text-slate-500">Date de demande</span>
        <span className="font-medium text-slate-800">
          {formatRequestDate(advance.requestDate)}
        </span>
      </div>
    </div>
  );
}
