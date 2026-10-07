import { deflateSync } from "node:zlib";
import { expect, type Page } from "@playwright/test";

/**
 * Espera rede, fontes e imagens: capturas só depois que a tela "assentou".
 * Imagens com loading="lazy" fora da tela nunca carregariam sozinhas (e capturas de página
 * inteira precisam delas), então são forçadas a carregar antes.
 */
export async function settle(page: Page) {
  await page.waitForLoadState("networkidle");
  await page.evaluate(async () => {
    await document.fonts.ready;
    const pending = Array.from(document.images).filter((img) => {
      img.loading = "eager";
      return !img.complete;
    });
    const loaded = Promise.all(
      pending.map(
        (img) =>
          new Promise((resolve) => {
            img.addEventListener("load", resolve, { once: true });
            img.addEventListener("error", resolve, { once: true });
          }),
      ),
    );
    await Promise.race([loaded, new Promise((resolve) => setTimeout(resolve, 10_000))]);
  });
  await page.waitForLoadState("networkidle");
}

/** Elementos que mudam a cada execução (datas, ids) ficam cobertos na comparação. */
export const volatile = (page: Page) => [page.locator("[data-volatile]")];

/** Cor da máscara: o "sand" do design system, para as imagens continuarem apresentáveis. */
export const maskColor = "#ede6db";

/** PNG de cor sólida gerado na hora (upload de imagem no backoffice). */
export function solidPng(width: number, height: number, [r, g, b]: [number, number, number]) {
  const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(width * 3, Buffer.from([r, g, b]))]);
  const raw = Buffer.concat(Array.from({ length: height }, () => row));
  const chunk = (type: string, data: Buffer) => {
    const body = Buffer.concat([Buffer.from(type), data]);
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, crc]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header.set([8, 2, 0, 0, 0], 8);
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", header),
    chunk("IDAT", deflateSync(raw)),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

function crc32(buffer: Buffer) {
  let crc = ~0;
  for (const byte of buffer) {
    crc ^= byte;
    for (let k = 0; k < 8; k++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return ~crc >>> 0;
}

export async function expectNoConsoleErrors(errors: string[]) {
  expect(errors, "erros no console do navegador").toEqual([]);
}
