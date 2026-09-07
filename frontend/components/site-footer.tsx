export function SiteFooter() {
  return (
    <footer className="w-full border-t border-border bg-muted/30">
      <div className="mx-auto flex max-w-screen-2xl flex-col items-center gap-2 px-6 py-8 text-center text-sm text-muted-foreground sm:flex-row sm:justify-between md:px-10">
        <span className="font-medium text-foreground/80">
          © {new Date().getFullYear()} arhia — conçu en stage chez AGIRH
        </span>
        <span>Assistant RH pour l&apos;onboarding et l&apos;offboarding des collaborateurs.</span>
      </div>
    </footer>
  );
}
