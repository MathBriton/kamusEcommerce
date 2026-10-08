import { defineConfig, devices } from "@playwright/test";

/**
 * Testes E2E e de regressão visual do Kamus.
 *
 * Rodam dentro do container oficial do Playwright contra uma stack isolada e com banco zerado
 * (ver e2e/run.sh). Assim navegador, fontes e dados são idênticos no CI e na máquina local, e as
 * capturas são determinísticas.
 *
 * As capturas de referência SÃO as imagens do design system: ficam em apps/web/design e
 * apps/admin/design. Uma tela que mudar sem querer faz o teste falhar; uma mudança intencional é
 * aceita com `./e2e/run.sh --update`.
 */
const desktop = { viewport: { width: 1280, height: 860 } };

export default defineConfig({
  testDir: "./tests",
  snapshotPathTemplate: "{testDir}/../../apps/{arg}{ext}",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  globalSetup: "./global-setup.ts",
  reporter: [["list"], ["html", { open: "never" }]],
  expect: {
    timeout: 15_000,
    // Ambiente idêntico em toda execução (mesmo container, banco zerado): tolerância mínima,
    // só para ruído de antialiasing. Uma palavra trocada já reprova.
    toHaveScreenshot: { maxDiffPixels: 50, animations: "disabled", caret: "hide" },
  },
  use: {
    ...devices["Desktop Chrome"],
    ...desktop,
    locale: "pt-BR",
    timezoneId: "America/Sao_Paulo",
    trace: "retain-on-failure",
  },
  // Ordem importa: a vitrine é capturada com o estoque intacto; o backoffice usa o pedido da compra.
  projects: [
    { name: "vitrine", testMatch: /vitrine\.spec\.ts/ },
    { name: "compra", testMatch: /compra\.spec\.ts/, dependencies: ["vitrine"] },
    {
      name: "backoffice",
      testMatch: /backoffice\.spec\.ts/,
      dependencies: ["compra"],
      // Campos de data seguem o idioma do Chromium (não o do contexto): dd/mm/aaaa como no Brasil.
      use: { viewport: { width: 1360, height: 900 }, launchOptions: { args: ["--lang=pt-BR"] } },
    },
  ],
});
