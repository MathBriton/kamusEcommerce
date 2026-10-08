import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { DeleteProductButton } from "./DeleteProductButton";

const router = vi.hoisted(() => ({ push: vi.fn(), refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));

// O jsdom ainda não implementa <dialog> modal: o mínimo para abrir, fechar e avisar o React.
beforeAll(() => {
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.setAttribute("open", "");
  };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
    this.removeAttribute("open");
    this.dispatchEvent(new Event("close"));
  };
});

afterEach(() => {
  vi.unstubAllGlobals();
  router.push.mockReset();
  router.refresh.mockReset();
});

const product = { id: "p-1", name: "Boné Trucker", skuCount: 2, imageCount: 1 };

describe("excluir produto", () => {
  it("pede confirmação explicando o que vai junto, com foco em Cancelar", () => {
    render(<DeleteProductButton product={product} variant="icon" />);

    fireEvent.click(screen.getByRole("button", { name: "Excluir Boné Trucker" }));

    const dialog = screen.getByRole("dialog", { name: "Excluir “Boné Trucker”?" });
    expect(dialog).toHaveAttribute("open");
    expect(dialog).toHaveTextContent("Os 2 SKUs e a imagem vão junto e voltam junto ao restaurar.");
    expect(screen.getByRole("button", { name: "Cancelar" })).toHaveFocus();
  });

  it("move para a lixeira e volta à lista com o aviso", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    render(<DeleteProductButton product={product} />);

    fireEvent.click(screen.getByRole("button", { name: "Excluir" }));
    fireEvent.click(screen.getByRole("button", { name: "Mover para a lixeira" }));

    await waitFor(() =>
      expect(router.push).toHaveBeenCalledWith("/produtos?excluido=Bon%C3%A9%20Trucker"),
    );
    expect(fetchMock).toHaveBeenCalledWith("/api/admin/catalog/products/p-1", {
      method: "DELETE",
    });
    expect(router.refresh).toHaveBeenCalled();
  });

  it("mostra o erro da API dentro do diálogo", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          Response.json(
            { title: "catalog.not_found", detail: "Produto não encontrado." },
            { status: 404 },
          ),
        ),
    );
    render(<DeleteProductButton product={product} />);

    fireEvent.click(screen.getByRole("button", { name: "Excluir" }));
    fireEvent.click(screen.getByRole("button", { name: "Mover para a lixeira" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Produto não encontrado.");
    expect(router.push).not.toHaveBeenCalled();
  });

  it("Cancelar fecha sem chamar a API", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    render(<DeleteProductButton product={product} />);

    fireEvent.click(screen.getByRole("button", { name: "Excluir" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
