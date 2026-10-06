import { describe, expect, it } from "vitest";
import { healthSchema } from "./health";

describe("healthSchema", () => {
  it("aceita o payload do /health da API", () => {
    const payload = {
      status: "Unhealthy",
      totalDurationMs: 12.5,
      checks: [
        {
          name: "redis",
          status: "Unhealthy",
          durationMs: 10,
          description: "Redis indisponível",
        },
      ],
    };

    expect(healthSchema.parse(payload).checks[0].description).toBe("Redis indisponível");
  });

  it("rejeita status desconhecido", () => {
    expect(healthSchema.safeParse({ status: "Ok", totalDurationMs: 1, checks: [] }).success).toBe(
      false,
    );
  });
});
