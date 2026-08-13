/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  Configuration Next.js — AGIRH frontend.
 *
 *  Durcissement HTTP (contrat XSS — Phase 4) :
 *   - En-têtes de sécurité appliqués UNIQUEMENT en production
 *     (`NODE_ENV === 'production'`, i.e. `next start` après `next build`).
 *   - En dev (`next dev`), aucune CSP : éviter de casser HMR / React
 *     DevTools / sourcemaps inline. La sécurité du dev reste assurée par la
 *     sanitisation Markdown stricte (`sanitizeSchema`) et l'absence de sinks
 *     HTML bruts dans `src/`.
 *
 *  Compatibilité Next.js prod :
 *   - `style-src 'unsafe-inline'` : requis pour le CSS injecté par Tailwind /
 *     styles inline générés par Next (le rendu statique des composants).
 *   - `script-src 'self'` : NOTE DE RISQUE — le RSC payload de Next.js App
 *     Router repose sur des scripts inline (`self.__next_f.push(...)`).
 *     `script-src 'self'` sans `'unsafe-inline'` ni nonce peut bloquer
 *     l'hydratation en prod. Vérifier au smoke test final ; si bloqué,
 *     passer à une stratégie nonce via middleware (Phase 4.1 conseillée).
 *     La valeur ci-dessous est conforme au contrat CSP demandé.
 * ─────────────────────────────────────────────────────────────────────────────
 */

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,

  /** Désactive le badge Next.js Dev Tools (bas-gauche, `next dev` uniquement) — cosmétique, sans impact sécurité. */
  devIndicators: false,

  /** En-têtes de sécurité — production uniquement. */
  async headers() {
    if (process.env.NODE_ENV !== 'production') {
      return [];
    }

    return [
      {
        source: '/:path*',
        headers: [
          {
            key: 'Content-Security-Policy',
            value: [
              "default-src 'self'",
              "script-src 'self'",
              "style-src 'self' 'unsafe-inline'",
              "img-src 'self' data: https:",
              "connect-src 'self'",
              "font-src 'self'",
              "frame-ancestors 'none'",
              "base-uri 'self'",
              "form-action 'self'",
            ].join('; '),
          },
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
          { key: 'X-Frame-Options', value: 'DENY' },
          {
            key: 'Permissions-Policy',
            value: 'camera=(), microphone=(), geolocation=()',
          },
        ],
      },
    ];
  },
};

export default nextConfig;
