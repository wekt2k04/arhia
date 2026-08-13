import type { Metadata } from 'next';

import './globals.css';

export const metadata: Metadata = {
  title: 'AGIRH — Assistant RH',
  description: 'Assistant RH intelligent AGIRH.',
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="fr">
      <body className="bg-slate-50 font-sans text-slate-900 antialiased">{children}</body>
    </html>
  );
}
