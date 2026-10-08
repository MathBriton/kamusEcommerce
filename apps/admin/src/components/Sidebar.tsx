"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { ICON_PATH } from "./icons";

type NavItem = { href: string; label: string; icon: string; badge?: number | null };

const NAV: NavItem[] = [
  {
    href: "/",
    label: "Painel",
    icon: "M3 13h8V3H3v10Zm0 8h8v-6H3v6Zm10 0h8V11h-8v10Zm0-18v6h8V3h-8Z",
  },
  {
    href: "/produtos",
    label: "Produtos",
    icon: "M20 7 12 3 4 7m16 0-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4",
  },
  {
    href: "/pedidos",
    label: "Pedidos",
    icon: "M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2M9 5a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2m-6 9 2 2 4-4",
  },
];

/** Menu lateral. `trashCount` vem do layout (servidor); null quando a contagem não pôde ser lida. */
export function Sidebar({
  name,
  storeUrl,
  trashCount = null,
}: {
  name: string;
  storeUrl: string;
  trashCount?: number | null;
}) {
  const pathname = usePathname();
  const router = useRouter();

  const control: NavItem[] = [
    { href: "/atividade", label: "Atividade", icon: ICON_PATH.history },
    { href: "/lixeira", label: "Lixeira", icon: ICON_PATH.trash, badge: trashCount },
  ];

  async function logout() {
    await fetch("/api/identity/logout", { method: "POST" });
    router.replace("/entrar");
    router.refresh();
  }

  return (
    <aside className="flex w-56 shrink-0 flex-col border-r border-line bg-surface">
      <div className="px-5 py-5">
        <p className="font-display text-xl tracking-[0.2em]">KAMUS</p>
        <p className="mt-0.5 text-xs text-muted">Backoffice</p>
      </div>
      <nav aria-label="Menu" className="flex-1 px-3">
        <ul className="space-y-0.5">
          {NAV.map((item) => (
            <NavLink key={item.href} item={item} pathname={pathname} />
          ))}
        </ul>
        <p
          id="menu-controle"
          className="mx-2.5 mt-3.5 mb-1 text-[11px] font-semibold tracking-[0.08em] text-muted uppercase"
        >
          Controle
        </p>
        <ul aria-labelledby="menu-controle" className="space-y-0.5">
          {control.map((item) => (
            <NavLink key={item.href} item={item} pathname={pathname} />
          ))}
        </ul>
      </nav>
      <div className="space-y-2 border-t border-line px-5 py-4 text-xs">
        <a
          href={storeUrl}
          target="_blank"
          rel="noreferrer"
          className="block text-muted hover:text-ink"
        >
          Abrir a loja ↗
        </a>
        <p className="truncate text-muted" title={name}>
          {name}
        </p>
        <button
          type="button"
          onClick={logout}
          className="text-muted underline underline-offset-4 hover:text-ink"
        >
          Sair
        </button>
      </div>
    </aside>
  );
}

function NavLink({ item, pathname }: { item: NavItem; pathname: string }) {
  const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
  const badge = item.badge ?? 0;
  return (
    <li>
      <Link
        href={item.href}
        aria-current={active ? "page" : undefined}
        className={`flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm ${active ? "bg-sand font-medium" : "text-muted hover:bg-paper hover:text-ink"}`}
      >
        <svg
          aria-hidden
          viewBox="0 0 24 24"
          className="size-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.7"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d={item.icon} />
        </svg>
        <span className="flex-1">{item.label}</span>
        {badge > 0 && (
          <span
            className={`tabular rounded-full px-[7px] py-px text-[11px] font-semibold text-ink ${active ? "bg-surface" : "bg-sand"}`}
          >
            {badge}
            <span className="sr-only"> {badge === 1 ? "item" : "itens"}</span>
          </span>
        )}
      </Link>
    </li>
  );
}
