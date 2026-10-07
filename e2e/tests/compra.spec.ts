import { expect, test, type Page } from "@playwright/test";
import { maskColor, settle, volatile } from "./helpers";
import { design, urls } from "./urls";

/**
 * Fluxo de compra completo, como um cliente faria, capturando cada tela.
 * Pedidos criados (banco zerado → números previsíveis):
 *  KM10001 — aprovado e entregue (simulação de envio na conta do cliente)
 *  KM10002 — cartão "sem resposta": fica aguardando pagamento
 *  KM10003 — aprovado e pago: o backoffice despacha
 */
test.describe.configure({ mode: "serial" });

const CUSTOMER = { name: "Maria Silva", email: "maria.silva@kamus.test", password: "senha-forte-123" };
const PRODUCT = "/masculino/calcas/jeans/calca-jeans-slim";
const CARD = { approved: "4242424242424242", timeout: "4000000000000119" };

let page: Page;
const shot = async (file: string, fullPage = false) => {
  // O mouse fica onde foi o último clique; tirá-lo da tela evita hovers e tooltips na captura.
  await page.mouse.move(0, 0);
  await expect(page).toHaveScreenshot(design("web", "telas", "compra", file), {
    fullPage,
    mask: volatile(page),
    maskColor,
  });
};

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage();
});

test.afterAll(async () => {
  await page.close();
});

test("adiciona à sacola na página do produto", async () => {
  await page.goto(`${urls.store}${PRODUCT}`);
  await settle(page);
  await page.locator("fieldset:has(legend:text('Tamanho')) button:not([disabled])").first().click();
  await page.getByRole("button", { name: "Adicionar à sacola" }).click();
  await expect(page.getByText("Adicionado à sacola.")).toBeVisible();
  await expect(page.locator("header a[href='/carrinho']")).toContainText("1");
  await shot("01-pdp-adicionado.png");
});

test("sacola", async () => {
  await page.getByRole("link", { name: "Ver sacola" }).click();
  await expect(page.getByRole("heading", { name: "Sacola" })).toBeVisible();
  await settle(page);
  await shot("02-sacola.png");
});

test("checkout pede login e cadastro mantém a sacola", async () => {
  await page.getByRole("link", { name: "Finalizar compra" }).click();
  await expect(page).toHaveURL(/\/entrar\?next=\/checkout/);
  await page.getByRole("link", { name: "Cadastre-se" }).click();
  await page.fill("#fullName", CUSTOMER.name);
  await page.fill("#email", CUSTOMER.email);
  await page.fill("#password", CUSTOMER.password);
  await page.getByRole("button", { name: "Criar conta" }).click();
  await expect(page).toHaveURL(/\/checkout$/);

  await page.fill("#postalCode", "01310-100");
  await page.fill("#street", "Avenida Paulista");
  await page.fill("#number", "1000");
  await page.fill("#district", "Bela Vista");
  await page.fill("#city", "São Paulo");
  await page.selectOption("#state", "SP");
  await expect(page.getByText(/Sudeste/)).toBeVisible();
  await settle(page);
  await shot("03-checkout.png");
});

test("pagamento aprovado chega por webhook e o pedido é pago", async () => {
  await page.getByRole("button", { name: "Pagar" }).click();
  await expect(page).toHaveURL(/\/conta\/pedidos\//);
  await expect(page.getByRole("heading", { name: "Pedido KM10001" })).toBeVisible();
  await expect(page.getByText("Pago", { exact: true }).first()).toBeVisible({ timeout: 20_000 });
  await expect(page.locator("header a[href='/carrinho']")).not.toContainText("1");
  await settle(page);
  await shot("05-pedido-pago.png");
});

test("envio e entrega aparecem no acompanhamento", async () => {
  await page.getByRole("button", { name: /Simular envio/ }).click();
  await page.getByRole("button", { name: /Simular entrega/ }).click();
  await expect(page.getByText("Entregue").first()).toBeVisible();
  await settle(page);
  await shot("06-pedido-entregue.png");
});

/** Compra direta pela API (mesma sessão do navegador), para preparar os próximos cenários. */
async function quickOrder(card: string) {
  const detail = await (await page.request.get(`${urls.store}/api/catalog/products/calca-chino`)).json();
  const sku = detail.colors
    .flatMap((c: { sizes: { skuId: string; available: number }[] }) => c.sizes)
    .find((s: { available: number }) => s.available > 0);
  await page.request.post(`${urls.store}/api/cart/items`, { data: { skuId: sku.skuId, quantity: 1 } });
  const checkout = await (await page.request.get(`${urls.store}/api/checkout`)).json();
  const shipping = await (
    await page.request.get(`${urls.store}/api/checkout/shipping?state=RJ&subtotal=${checkout.subtotal}`)
  ).json();
  const response = await page.request.post(`${urls.store}/api/orders`, {
    data: {
      address: {
        recipientName: CUSTOMER.name,
        postalCode: "22041001",
        street: "Avenida Atlântica",
        number: "500",
        complement: null,
        district: "Copacabana",
        city: "Rio de Janeiro",
        state: "RJ",
      },
      cardNumber: card,
      expectedTotal: checkout.subtotal + shipping.cost,
    },
  });
  expect(response.status()).toBe(201);
  return (await response.json()) as { id: string; number: string };
}

test("pagamento sem resposta deixa o pedido aguardando", async () => {
  const order = await quickOrder(CARD.timeout);
  expect(order.number).toBe("KM10002");
  await page.goto(`${urls.store}/conta/pedidos/${order.id}`);
  await settle(page);
  await expect(page.getByText("Aguardando pagamento").first()).toBeVisible();
  await shot("04-pedido-aguardando.png");
});

test("terceiro pedido fica pago para o backoffice despachar", async () => {
  const order = await quickOrder(CARD.approved);
  expect(order.number).toBe("KM10003");
  await expect
    .poll(
      async () => (await (await page.request.get(`${urls.store}/api/orders/${order.id}`)).json()).status,
      { timeout: 20_000 },
    )
    .toBe("Paid");
});

test("minha conta lista os pedidos", async () => {
  await page.goto(`${urls.store}/conta`);
  await expect(page.locator("a[href^='/conta/pedidos/']")).toHaveCount(3);
  await settle(page);
  await shot("07-minha-conta.png");
});
