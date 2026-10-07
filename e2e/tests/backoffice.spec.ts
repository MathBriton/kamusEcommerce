import { expect, test, type Page } from "@playwright/test";
import { maskColor, settle, solidPng, volatile } from "./helpers";
import { admin, design, urls } from "./urls";

/** Operação da loja pelo backoffice, capturando cada tela. Usa os pedidos criados em compra.spec.ts. */
test.describe.configure({ mode: "serial" });

let page: Page;
const shot = async (file: string, fullPage = false) => {
  // O mouse fica onde foi o último clique; tirá-lo da tela evita hovers e tooltips na captura.
  await page.mouse.move(0, 0);
  await expect(page).toHaveScreenshot(design("admin", "telas", file), {
    fullPage,
    mask: volatile(page),
    maskColor,
  });
};

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage({ viewport: { width: 1360, height: 900 } });
});

test.afterAll(async () => {
  await page.close();
});

test("login exige administrador", async () => {
  await page.goto(`${urls.admin}/`);
  await expect(page).toHaveURL(/\/entrar/);
  await settle(page);
  await shot("01-login.png");

  // um cliente comum não entra
  await page.fill("#email", "maria.silva@kamus.test");
  await page.fill("#password", "senha-forte-123");
  await page.getByRole("button", { name: "Entrar" }).click();
  await expect(page.getByText("Sua conta não tem acesso ao backoffice.")).toBeVisible();

  await page.fill("#email", admin.email);
  await page.fill("#password", admin.password);
  await page.getByRole("button", { name: "Entrar" }).click();
  await expect(page.getByRole("heading", { name: "Painel" })).toBeVisible();
});

test("painel mostra os números da compra", async () => {
  await settle(page);
  await expect(page.getByText("2 pedidos pagos")).toBeVisible();
  await expect(page.getByText("de 3 pedidos criados")).toBeVisible();
  await shot("02-painel.png", true);
});

test("lista de produtos", async () => {
  await page.getByRole("link", { name: "Produtos" }).click();
  await expect(page.getByRole("heading", { name: "Produtos" })).toBeVisible();
  await settle(page);
  await shot("03-produtos.png");
});

test("cria, prepara e publica um produto", async () => {
  await page.getByRole("link", { name: "Novo produto" }).click();
  await page.fill("#name", "Camisa de Linho Areia");
  await page.selectOption("#categoryId", { label: "Masculino › Camisas" });
  await page.fill("#description", "Camisa de linho puro com caimento solto, ideal para dias quentes.");
  await page.getByRole("button", { name: "Criar rascunho" }).click();
  await expect(page.getByRole("heading", { name: "Camisa de Linho Areia" })).toBeVisible();

  await page.getByRole("button", { name: "Publicar" }).click();
  await expect(page.locator("p[role=alert]")).toHaveText(
    "Cadastre ao menos um SKU antes de publicar o produto.",
  );

  await page.fill("#color", "Areia");
  await page.locator("#colorHex").fill("#d9c7a7");
  await page.fill("#sizes", "P M G GG");
  await page.fill("#price", "289.90");
  await page.fill("#salePrice", "249.90");
  await page.fill("#initialStock", "6");
  await page.getByRole("button", { name: "Adicionar variações" }).click();
  await expect(page.getByText("Variações adicionadas.")).toBeVisible();

  await page.setInputFiles("input[type=file]", {
    name: "areia.png",
    mimeType: "image/png",
    buffer: solidPng(300, 400, [217, 199, 167]),
  });
  await expect(page.getByText("Imagem enviada.")).toBeVisible();

  await page.getByRole("button", { name: "Publicar" }).click();
  await expect(page.getByText("Produto publicado na loja.")).toBeVisible();

  await page.getByLabel(/Estoque físico de .*-P$/).fill("2");
  await page
    .getByRole("row", { name: /Areia · P / })
    .getByRole("button", { name: "Salvar" })
    .click();
  await expect(page.getByText("Estoque de Areia · P ajustado.")).toBeVisible();
  await settle(page);
  await shot("04-produto-editor.png", true);

  // o produto está na loja, com o estoque ajustado
  const pdp = await page.request.get(`${urls.store}/api/catalog/products/camisa-de-linho-areia`);
  expect(pdp.ok()).toBeTruthy();
  const sizes = (await pdp.json()).colors[0].sizes.map((s: { size: string; available: number }) => [
    s.size,
    s.available,
  ]);
  expect(sizes).toEqual([
    ["P", 2],
    ["M", 6],
    ["G", 6],
    ["GG", 6],
  ]);
});

test("lista de pedidos pagos", async () => {
  await page.getByRole("link", { name: "Pedidos" }).click();
  await page.getByRole("link", { name: "Pago", exact: true }).click();
  await expect(page.getByRole("link", { name: "KM10003" })).toBeVisible();
  await settle(page);
  await shot("05-pedidos.png");
});

test("despacha o pedido com rastreio", async () => {
  await page.getByRole("link", { name: "KM10003" }).click();
  await page.fill("#trackingCode", "BR123456789BR");
  await page.getByRole("button", { name: "Despachar pedido" }).click();
  await expect(page.getByRole("button", { name: "Confirmar entrega" })).toBeVisible();
  await expect(page.getByText("Rastreio: BR123456789BR").first()).toBeVisible();
  await settle(page);
  await shot("06-pedido-despachado.png", true);
});
