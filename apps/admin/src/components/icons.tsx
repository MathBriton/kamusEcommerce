/** Ícones de traço (24×24, mesma espessura do menu). Decorativos: o texto ou aria-label nomeia a ação. */

export const ICON_PATH = {
  trash: "M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V4h6v3",
  history: "M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5M12 7v5l3 2",
  restore: "M9 14 4 9l5-5M4 9h10a6 6 0 0 1 0 12h-3",
  download: "M12 4v12m0 0-4-4m4 4 4-4M5 20h14",
  chevronDown: "m6 9 6 6 6-6",
  lock: "M7 11V8a5 5 0 0 1 10 0v3M6 11h12v9H6z",
  check: "m5 12 5 5 9-10",
  photo: "M4 5h16v14H4zM7 16l4-5 3 3 2-2 3 4z",
} as const;

export function Icon({
  name,
  className = "size-4",
  strokeWidth = 1.7,
}: {
  name: keyof typeof ICON_PATH;
  className?: string;
  strokeWidth?: number;
}) {
  return (
    <svg
      aria-hidden
      viewBox="0 0 24 24"
      className={`shrink-0 ${className}`}
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d={ICON_PATH[name]} />
    </svg>
  );
}
