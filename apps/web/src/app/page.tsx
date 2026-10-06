import { connection } from "next/server";
import { HealthStatus } from "@/components/HealthStatus";
import { getHealth } from "@/lib/health";

export default async function Home() {
  await connection();
  const health = await getHealth();

  return (
    <div className="mx-auto max-w-6xl px-4 py-16">
      <section className="mb-12 max-w-2xl">
        <p className="mb-3 text-sm tracking-widest text-accent uppercase">Em breve</p>
        <h1 className="mb-4 font-display text-5xl leading-tight">Moda com sotaque brasileiro.</h1>
        <p className="text-lg text-muted">
          A vitrine está sendo montada. Enquanto isso, este é o status da plataforma.
        </p>
      </section>
      <section className="max-w-sm">
        <HealthStatus result={health} />
      </section>
    </div>
  );
}
