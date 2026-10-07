import { expect, test } from "@playwright/test";
import { maskColor, settle } from "./helpers";
import { design, urls } from "./urls";

/**
 * Vitrine: guia de design (fundamentos e componentes) e páginas públicas, em desktop e mobile.
 * Roda primeiro, com o estoque do seed intacto.
 */

const SECTIONS = [
  ["marca", "fundamentos", "marca"],
  ["cores", "fundamentos", "cores"],
  ["tipografia", "fundamentos", "tipografia"],
  ["botoes", "componentes", "botoes-e-links"],
  ["formularios", "componentes", "formularios"],
  ["selecao", "componentes", "selecao-de-variacao"],
  ["precos-e-status", "componentes", "precos-e-status"],
  ["card-de-produto", "componentes", "card-de-produto"],
  ["ilustracoes", "componentes", "ilustracoes-das-pecas"],
] as const;

test("guia de design: fundamentos e componentes", async ({ page }) => {
  await page.setViewportSize({ width: 1100, height: 900 });
  await page.goto(`${urls.store}/design-system`);
  await settle(page);

  for (const [id, folder, file] of SECTIONS) {
    await expect(page.locator(`#${id}`)).toHaveScreenshot(design("web", folder, `${file}.png`));
  }
});

const PAGES = [
  ["home", "/"],
  ["listagem", "/feminino"],
  ["produto", "/masculino/calcas/jeans/calca-jeans-slim"],
] as const;

for (const [name, path] of PAGES) {
  test(`tela ${name} (desktop)`, async ({ page }) => {
    await page.goto(`${urls.store}${path}`);
    await settle(page);
    await expect(page).toHaveScreenshot(design("web", "telas", "vitrine", `${name}-desktop.png`), {
      maskColor,
    });
  });
}

test.describe("mobile", () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  for (const [name, path] of PAGES) {
    test(`tela ${name} (mobile)`, async ({ page }) => {
      await page.goto(`${urls.store}${path}`);
      await settle(page);
      await expect(page).toHaveScreenshot(design("web", "telas", "vitrine", `${name}-mobile.png`), {
        maskColor,
      });
    });
  }
});

test("filtros da listagem atualizam a URL e os produtos", async ({ page }) => {
  await page.goto(`${urls.store}/feminino`);
  await settle(page);
  const before = await page.locator("article").count();

  await page.getByRole("button", { name: "38", exact: true }).click();
  await page.getByLabel("Preto").check();

  await expect(page).toHaveURL(/tamanho=38/);
  await expect(page).toHaveURL(/cor=Preto/);
  await expect.poll(() => page.locator("article").count()).toBeLessThan(before);
});

test("sitemap e robots publicados", async ({ request }) => {
  const sitemap = await request.get(`${urls.store}/sitemap.xml`);
  expect(sitemap.ok()).toBeTruthy();
  expect(await sitemap.text()).toContain("/masculino/calcas/jeans/calca-jeans-slim");

  const robots = await (await request.get(`${urls.store}/robots.txt`)).text();
  expect(robots).toContain("Disallow: /design-system");
});
