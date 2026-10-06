import type { Metadata } from "next";
import { connection } from "next/server";
import { HealthStatus } from "@/components/HealthStatus";
import { getHealth } from "@/lib/health";

export const metadata: Metadata = { title: "Status", robots: { index: false } };

export default async function StatusPage() {
  await connection();
  const health = await getHealth();

  return (
    <div className="mx-auto max-w-6xl px-4 py-16">
      <h1 className="mb-6 font-display text-4xl">Status da plataforma</h1>
      <div className="max-w-sm">
        <HealthStatus result={health} />
      </div>
    </div>
  );
}
