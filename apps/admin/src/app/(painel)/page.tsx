import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { DailyRevenueChart } from "@/components/charts/DailyRevenueChart";
import { StatusBars } from "@/components/charts/StatusBars";
import { StatTile } from "@/components/StatTile";
import { Card, PageHeader } from "@/components/ui";
import { formatDay, formatInt, formatPercent, formatPrice } from "@/lib/format";
import { overviewSchema } from "@/lib/schemas";
import { apiGetWithSession } from "@/lib/server-api";

export const metadata: Metadata = { title: "Painel" };

const PRESETS = [7, 30, 90] as const;

/** "Hoje" no fuso da loja, para os presets de período. */
function todayInBrazil() {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "America/Sao_Paulo" }).format(new Date());
}

function addDays(isoDate: string, days: number) {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

export default async function DashboardPage(props: PageProps<"/">) {
  const { dias } = await props.searchParams;
  const days = PRESETS.find((p) => String(p) === dias) ?? 30;
  const to = todayInBrazil();
  const report = (await apiGetWithSession(
    `/api/admin/reports/overview?from=${addDays(to, -(days - 1))}&to=${to}`,
    overviewSchema,
  ))!;
  const { kpis } = report;

  return (
    <>
      <PageHeader
        title="Painel"
        description={`${formatDay(report.period.from)} a ${formatDay(report.period.to)} · horário de Brasília`}
      />

      {/* Filtro de período: uma linha, acima de tudo o que ele afeta. */}
      <nav
        aria-label="Período"
        className="mb-6 flex w-fit gap-1 rounded-md border border-line bg-surface p-1 text-sm"
      >
        {PRESETS.map((p) => (
          <Link
            key={p}
            href={p === 30 ? "/" : `/?dias=${p}`}
            aria-current={p === days ? "true" : undefined}
            className={`rounded px-3 py-1 ${p === days ? "bg-ink text-paper" : "text-muted hover:text-ink"}`}
          >
            Últimos {p} dias
          </Link>
        ))}
      </nav>

      <div className="mb-6 grid gap-4 lg:grid-cols-[1.4fr_1fr_1fr]">
        <StatTile
          hero
          label="Faturamento"
          value={formatPrice(kpis.revenue)}
          hint={`${formatInt(kpis.paidOrders)} pedidos pagos`}
        />
        <div className="grid gap-4">
          <StatTile label="Ticket médio" value={formatPrice(kpis.averageTicket)} />
          <StatTile label="Peças vendidas" value={formatInt(kpis.unitsSold)} />
        </div>
        <div className="grid gap-4">
          <StatTile
            label="Pagamentos recusados"
            value={formatPercent(kpis.paymentFailureRate)}
            hint="dos pagamentos concluídos"
          />
          <StatTile
            label="Pedidos cancelados"
            value={formatPercent(kpis.cancellationRate)}
            hint={`de ${formatInt(kpis.totalOrders)} pedidos criados`}
          />
        </div>
      </div>

      <div className="mb-6 grid gap-6 lg:grid-cols-[2fr_1fr]">
        <Card title="Faturamento por dia" className="min-w-0">
          <DailyRevenueChart data={report.daily} />
        </Card>
        <Card title="Pedidos por status">
          <StatusBars data={report.statuses} />
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card title="Mais vendidos">
          {report.topProducts.length === 0 ? (
            <p className="text-sm text-muted">Nenhuma venda no período.</p>
          ) : (
            <ol className="divide-y divide-line text-sm">
              {report.topProducts.map((p, i) => (
                <li key={p.skuId} className="flex items-center gap-3 py-2.5">
                  <span className="tabular w-4 text-xs text-muted">{i + 1}</span>
                  <div className="relative h-12 w-9 shrink-0 overflow-hidden rounded bg-sand">
                    {p.imageUrl && (
                      <Image
                        src={p.imageUrl}
                        alt=""
                        fill
                        unoptimized
                        sizes="36px"
                        className="object-cover"
                      />
                    )}
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{p.productName}</p>
                    <p className="text-xs text-muted">
                      {p.color} · {p.size === "U" ? "Único" : p.size}
                    </p>
                  </div>
                  <div className="tabular text-right">
                    <p className="font-medium">{formatInt(p.units)} un.</p>
                    <p className="text-xs text-muted">{formatPrice(p.revenue)}</p>
                  </div>
                </li>
              ))}
            </ol>
          )}
        </Card>

        <Card
          title="Estoque baixo"
          actions={<span className="text-xs text-muted">disponível ≤ 3</span>}
        >
          {report.lowStock.length === 0 ? (
            <p className="text-sm text-muted">Nenhum item com estoque baixo.</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="text-left text-xs text-muted">
                <tr>
                  <th className="pb-2 font-medium">Item</th>
                  <th className="pb-2 text-right font-medium">Físico</th>
                  <th className="pb-2 text-right font-medium">Reservado</th>
                  <th className="pb-2 text-right font-medium">Disponível</th>
                </tr>
              </thead>
              <tbody className="tabular divide-y divide-line">
                {report.lowStock.map((s) => (
                  <tr key={s.skuId}>
                    <td className="py-2">
                      <p className="font-medium">{s.productName}</p>
                      <p className="text-xs text-muted">
                        {s.color} · {s.size === "U" ? "Único" : s.size}
                      </p>
                    </td>
                    <td className="py-2 text-right">{s.quantity}</td>
                    <td className="py-2 text-right">{s.reserved}</td>
                    <td
                      className={`py-2 text-right font-medium ${s.available === 0 ? "text-sale" : ""}`}
                    >
                      {s.available === 0 ? "Esgotado" : s.available}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      </div>
    </>
  );
}
