import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HealthStatus } from "./HealthStatus";

describe("HealthStatus", () => {
  it("mostra o status de cada dependência", () => {
    render(
      <HealthStatus
        result={{
          ok: true,
          health: {
            status: "Healthy",
            totalDurationMs: 3.2,
            checks: [
              { name: "postgres", status: "Healthy", durationMs: 2 },
              { name: "redis", status: "Healthy", durationMs: 1 },
            ],
          },
        }}
      />,
    );

    expect(screen.getByText(/API: Healthy/)).toBeInTheDocument();
    expect(screen.getByText("postgres: Healthy")).toBeInTheDocument();
    expect(screen.getByText("redis: Healthy")).toBeInTheDocument();
  });

  it("mostra erro quando a API está fora do ar", () => {
    render(<HealthStatus result={{ ok: false, error: "API indisponível" }} />);

    expect(screen.getByRole("status")).toHaveTextContent("API indisponível");
  });
});
