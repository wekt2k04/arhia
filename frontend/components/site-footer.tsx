export function SiteFooter() {
  return (
    <footer className="w-full border-t border-border">
      <div className="mx-auto flex max-w-5xl flex-col items-center gap-1 px-6 py-6 text-center text-xs text-muted-foreground sm:flex-row sm:justify-between">
        <span>© {new Date().getFullYear()} AGIRH</span>
        <span>Assistant RH pour l&apos;onboarding et l&apos;offboarding des collaborateurs.</span>
      </div>
    </footer>
  );
}
