"use client";

const TOKENS = [
  ["ink", "Texto, botões primários"],
  ["paper", "Fundo da página"],
  ["surface", "Cartões, campos"],
  ["sand", "Áreas de destaque, fundo de imagem"],
  ["line", "Bordas e divisórias"],
  ["muted", "Texto secundário"],
  ["accent", "Terracota: links, hover, CTA"],
  ["accent-dark", "Terracota escuro"],
  ["sale", "Preço promocional, erros"],
] as const;

/** Mostra o valor real de um token (packages/tokens/theme.css), lido do CSS já aplicado. */
function TokenValue({ name }: { name: string }) {
  return (
    <span
      ref={(el) => {
        if (el) el.textContent = getComputedStyle(el).getPropertyValue(`--color-${name}`).trim();
      }}
    />
  );
}

/** Amostras de cor sem duplicar os hex aqui: a fonte é o tema compartilhado. */
export function ColorSwatches() {
  return (
    <div className="grid grid-cols-3 gap-4">
      {TOKENS.map(([name, use]) => (
        <div key={name} className="overflow-hidden border border-line bg-surface">
          <div className="h-24" style={{ backgroundColor: `var(--color-${name})` }} />
          <div className="p-3 text-sm">
            <p className="font-medium">{name}</p>
            <p className="font-mono text-xs text-muted">
              <TokenValue name={name} />
            </p>
            <p className="mt-1 text-xs text-muted">{use}</p>
          </div>
        </div>
      ))}
    </div>
  );
}
