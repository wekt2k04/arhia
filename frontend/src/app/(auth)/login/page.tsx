'use client';

import { Suspense, useEffect, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';

type ErrorKind = 'generic' | 'rate' | 'unavailable';

interface FormError {
  kind: ErrorKind;
  message: string;
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();

  // Anti open-redirect : seul un chemin relatif interne (commençant par `/`
  // mais PAS `//`) est accepté. Tout le reste retombe sur `/chat`.
  const returnUrl = useMemo(() => {
    const raw = searchParams.get('returnUrl');
    if (!raw) return null;
    if (!raw.startsWith('/') || raw.startsWith('//')) return null;
    return raw;
  }, [searchParams]);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<FormError | null>(null);

  // Session déjà active → on n'affiche pas le formulaire, on redirige.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const res = await fetch('/api/auth/session', { cache: 'no-store' });
        if (!cancelled && res.ok) {
          router.replace('/chat');
        }
      } catch {
        // La vérification est best-effort : en cas d'échec réseau, le
        // formulaire reste affiché et la connexion reste possible.
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [router]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const normalizedEmail = email.trim().toLowerCase();

    if (!normalizedEmail || !EMAIL_RE.test(normalizedEmail)) {
      setError({ kind: 'generic', message: 'Veuillez saisir une adresse email valide.' });
      return;
    }

    if (!password.trim()) {
      setError({ kind: 'generic', message: 'Veuillez saisir votre mot de passe.' });
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: normalizedEmail, password }),
      });

      if (res.ok) {
        router.push(returnUrl ?? '/chat');
        return;
      }

      if (res.status === 429) {
        setError({ kind: 'rate', message: 'Trop de tentatives de connexion, réessayez plus tard.' });
      } else if (res.status === 503) {
        setError({ kind: 'unavailable', message: 'Service temporairement indisponible, réessayez plus tard.' });
      } else {
        setError({ kind: 'generic', message: 'Email ou mot de passe invalide.' });
      }
    } catch {
      setError({ kind: 'unavailable', message: 'Service temporairement indisponible, réessayez plus tard.' });
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 p-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="mb-1 text-2xl font-semibold text-slate-900">Connexion</h1>
        <p className="mb-6 text-sm text-slate-500">Espace RH AGIRH</p>

        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          <div>
            <label htmlFor="email" className="mb-1 block text-sm font-medium text-slate-700">
              Adresse email
            </label>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              disabled={loading}
              placeholder="vous@agirh.fr"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-60"
            />
          </div>

          <div>
            <label htmlFor="password" className="mb-1 block text-sm font-medium text-slate-700">
              Mot de passe
            </label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              disabled={loading}
              placeholder="••••••••"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-60"
            />
          </div>

          {error ? (
            <p
              role="alert"
              className={
                error.kind === 'rate'
                  ? 'rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800'
                  : 'rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700'
              }
            >
              {error.message}
            </p>
          ) : null}

          <button
            type="submit"
            disabled={loading}
            className="w-full rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {loading ? 'Connexion en cours…' : 'Se connecter'}
          </button>
        </form>
      </div>
    </main>
  );
}

export default function LoginPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center text-sm text-slate-500">
          Chargement…
        </div>
      }
    >
      <LoginForm />
    </Suspense>
  );
}
