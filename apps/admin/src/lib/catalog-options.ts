import "server-only";
import { z } from "zod";
import { apiGetWithSession } from "./server-api";
import { categoryNodeSchema, collectionSchema, type CategoryNode } from "./schemas";

export type CategoryOption = { id: string; label: string };

/** Categorias "folha" com o caminho completo, para o select do formulário de produto. */
export async function loadCatalogOptions() {
  const [tree, collections] = await Promise.all([
    apiGetWithSession("/api/catalog/categories", z.array(categoryNodeSchema)),
    apiGetWithSession("/api/admin/catalog/collections", z.array(collectionSchema)),
  ]);

  const options: CategoryOption[] = [];
  const walk = (nodes: CategoryNode[], trail: string[]) =>
    nodes.forEach((n) => {
      const path = [...trail, n.name];
      if (n.children.length === 0) options.push({ id: n.id, label: path.join(" › ") });
      walk(n.children, path);
    });
  walk(tree ?? [], []);

  return { categories: options, collections: collections ?? [] };
}
