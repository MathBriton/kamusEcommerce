import { describe, expect, it } from "vitest";
import { formatDate, formatDateTime, formatTime } from "./format";

describe("datas no fuso da loja", () => {
  // 19:42 UTC = 16:42 em Brasília (UTC−3, sem horário de verão).
  const iso = "2026-10-07T19:42:00Z";

  it("data e hora separadas, como na Atividade e na Lixeira", () => {
    expect(formatDate(iso)).toBe("07/10/2026");
    expect(formatTime(iso)).toBe("16:42");
  });

  it("data e hora juntas não dependem do fuso do servidor", () => {
    expect(formatDateTime(iso)).toBe("07/10/2026, 16:42");
  });

  it("perto da meia-noite UTC ainda é o dia anterior em Brasília", () => {
    expect(formatDate("2026-10-08T01:30:00Z")).toBe("07/10/2026");
  });
});
