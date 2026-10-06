import type { HealthResult } from "@/lib/health";

const dot: Record<string, string> = {
  Healthy: "bg-emerald-500",
  Degraded: "bg-amber-500",
  Unhealthy: "bg-red-500",
};

export function HealthStatus({ result }: { result: HealthResult }) {
  if (!result.ok) {
    return (
      <div role="status" className="rounded-md border border-red-200 bg-red-50 p-4 text-sm">
        <span className="font-medium text-red-700">API: {result.error}</span>
      </div>
    );
  }

  const { health } = result;

  return (
    <div role="status" className="rounded-md border border-line bg-surface p-4 text-sm">
      <p className="mb-2 flex items-center gap-2 font-medium">
        <span className={`size-2 rounded-full ${dot[health.status]}`} />
        API: {health.status}
        <span className="text-muted">({health.totalDurationMs} ms)</span>
      </p>
      <ul className="space-y-1 text-muted">
        {health.checks.map((check) => (
          <li key={check.name} className="flex items-center gap-2">
            <span className={`size-1.5 rounded-full ${dot[check.status]}`} />
            {check.name}: {check.status}
          </li>
        ))}
      </ul>
    </div>
  );
}
