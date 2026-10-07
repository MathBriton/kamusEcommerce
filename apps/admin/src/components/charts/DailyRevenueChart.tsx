"use client";

import { useEffect, useRef, useState } from "react";
import { formatDay, formatInt, formatPrice, formatPriceCompact } from "@/lib/format";

type Point = { date: string; revenue: number; orders: number };

const HEIGHT = 240;
const PAD = { top: 12, right: 8, bottom: 28, left: 64 };
const MAX_BAR = 24;

/** Escala "limpa" para o eixo Y: 0, 1/2/2,5/5 × 10^n. */
function niceTicks(max: number, count = 4): number[] {
  if (max <= 0) return [0, 1];
  const raw = max / count;
  const magnitude = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((s) => s >= raw)!;
  return Array.from({ length: Math.ceil(max / step) + 1 }, (_, i) => i * step);
}

/**
 * Faturamento por dia: colunas de uma única série (a cor de destaque da marca).
 * Sem legenda — o título já diz o que é; tooltip por coluna e visão em tabela.
 */
export function DailyRevenueChart({ data }: { data: Point[] }) {
  const ref = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(0);
  const [hover, setHover] = useState<number | null>(null);
  const [showTable, setShowTable] = useState(false);

  useEffect(() => {
    const element = ref.current;
    if (!element) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const ticks = niceTicks(Math.max(...data.map((d) => d.revenue)));
  const top = ticks[ticks.length - 1];
  const plotW = Math.max(0, width - PAD.left - PAD.right);
  const plotH = HEIGHT - PAD.top - PAD.bottom;
  const slot = plotW / Math.max(1, data.length);
  const barW = Math.min(MAX_BAR, Math.max(2, slot - 2)); // 2px de ar entre colunas vizinhas
  const y = (v: number) => PAD.top + plotH - (v / top) * plotH;
  const labelEvery = Math.ceil(data.length / Math.max(1, Math.floor(plotW / 64)));
  const hovered = hover === null ? null : data[hover];

  return (
    <div>
      <div className="mb-2 flex justify-end">
        <button
          type="button"
          onClick={() => setShowTable((v) => !v)}
          className="text-xs text-muted underline underline-offset-4 hover:text-ink"
        >
          {showTable ? "Ver gráfico" : "Ver tabela"}
        </button>
      </div>

      {showTable ? (
        <div className="max-h-72 overflow-y-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-xs text-muted">
              <tr>
                <th className="py-1 font-medium">Dia</th>
                <th className="py-1 text-right font-medium">Pedidos</th>
                <th className="py-1 text-right font-medium">Faturamento</th>
              </tr>
            </thead>
            <tbody className="tabular divide-y divide-line">
              {data.map((d) => (
                <tr key={d.date}>
                  <td className="py-1">{formatDay(d.date)}</td>
                  <td className="py-1 text-right">{formatInt(d.orders)}</td>
                  <td className="py-1 text-right">{formatPrice(d.revenue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div
          ref={ref}
          className="relative min-w-0"
          style={{ height: HEIGHT }}
          onPointerLeave={() => setHover(null)}
        >
          {width > 0 && (
            <svg
              width={width}
              height={HEIGHT}
              role="img"
              aria-label="Faturamento por dia no período"
            >
              {ticks.map((t) => (
                <g key={t}>
                  <line
                    x1={PAD.left}
                    x2={width - PAD.right}
                    y1={y(t)}
                    y2={y(t)}
                    stroke="var(--color-line)"
                    strokeWidth={1}
                  />
                  <text
                    x={PAD.left - 8}
                    y={y(t)}
                    dy="0.32em"
                    textAnchor="end"
                    className="tabular fill-muted text-[11px]"
                  >
                    {formatPriceCompact(t)}
                  </text>
                </g>
              ))}

              {data.map((d, i) => {
                const x = PAD.left + i * slot + (slot - barW) / 2;
                const h = Math.max(0, y(0) - y(d.revenue));
                const r = Math.min(4, barW / 2, h);
                // Ponta arredondada (4px) e base reta, ancorada na linha de base.
                const path =
                  h <= 0
                    ? ""
                    : `M${x},${y(0)} V${y(0) - h + r} Q${x},${y(0) - h} ${x + r},${y(0) - h} H${x + barW - r} Q${x + barW},${y(0) - h} ${x + barW},${y(0) - h + r} V${y(0)} Z`;
                return (
                  <g key={d.date}>
                    {path && (
                      <path
                        d={path}
                        fill="var(--color-accent)"
                        opacity={hover === null || hover === i ? 1 : 0.55}
                      />
                    )}
                    {/* Área de hover maior que a coluna: a fatia inteira do dia. */}
                    <rect
                      x={PAD.left + i * slot}
                      y={PAD.top}
                      width={slot}
                      height={plotH}
                      fill="transparent"
                      tabIndex={0}
                      aria-label={`${formatDay(d.date)}: ${formatPrice(d.revenue)}, ${d.orders} pedidos`}
                      onPointerEnter={() => setHover(i)}
                      onFocus={() => setHover(i)}
                      onBlur={() => setHover(null)}
                      className="outline-none"
                    />
                    {i % labelEvery === 0 && (
                      <text
                        x={PAD.left + i * slot + slot / 2}
                        y={HEIGHT - 8}
                        textAnchor="middle"
                        className="fill-muted text-[11px]"
                      >
                        {formatDay(d.date)}
                      </text>
                    )}
                  </g>
                );
              })}
              <line
                x1={PAD.left}
                x2={width - PAD.right}
                y1={y(0)}
                y2={y(0)}
                stroke="var(--color-muted)"
                strokeWidth={1}
              />
            </svg>
          )}

          {hovered && hover !== null && (
            <div
              role="tooltip"
              className="pointer-events-none absolute z-10 min-w-36 -translate-x-1/2 rounded-md border border-line bg-surface px-3 py-2 text-xs shadow-md"
              style={{
                left: Math.min(Math.max(PAD.left + hover * slot + slot / 2, 80), width - 80),
                top: 0,
              }}
            >
              <p className="tabular text-sm font-semibold">{formatPrice(hovered.revenue)}</p>
              <p className="text-muted">
                {formatDay(hovered.date)} · {hovered.orders}{" "}
                {hovered.orders === 1 ? "pedido" : "pedidos"}
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
