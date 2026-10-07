/** Indicador numérico: rótulo, valor e uma linha de contexto opcional. */
export function StatTile({
  label,
  value,
  hint,
  hero = false,
}: {
  label: string;
  value: string;
  hint?: string;
  hero?: boolean;
}) {
  return (
    <div className="rounded-lg border border-line bg-surface px-5 py-4">
      <p className="text-xs font-medium text-muted">{label}</p>
      <p className={`mt-1 font-semibold tracking-tight ${hero ? "text-5xl" : "text-2xl"}`}>
        {value}
      </p>
      {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
    </div>
  );
}
