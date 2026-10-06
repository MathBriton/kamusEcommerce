import Link from "next/link";

export function SiteHeader() {
  return (
    <header className="border-b border-line bg-surface">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4">
        <Link href="/" className="font-display text-2xl tracking-[0.2em]">
          KAMUS
        </Link>
      </div>
    </header>
  );
}
