import { NextResponse, type NextRequest } from "next/server";
import { apiUrl } from "@/lib/env";

/**
 * As URLs públicas do catálogo não têm prefixo: `/masculino/calcas` é uma PLP e
 * `/masculino/calcas/jeans-slim` é uma PDP. Como cada uma usa uma estratégia de renderização
 * (ISR vs. SSR, ver ADR 0006), o proxy reescreve internamente para `/categoria/...` ou `/produto/...`.
 *
 * O conjunto de caminhos de categoria é pequeno e fica em memória com TTL curto.
 */
const TTL_MS = 60_000;
let cache: { paths: Set<string>; expiresAt: number } | undefined;
let inflight: Promise<Set<string>> | undefined;

type Node = { path: string; children: Node[] };

async function loadCategoryPaths(): Promise<Set<string>> {
  const response = await fetch(`${apiUrl}/api/catalog/categories`, {
    signal: AbortSignal.timeout(2000),
  });
  if (!response.ok) throw new Error(`categories ${response.status}`);
  const tree = (await response.json()) as Node[];
  const paths = new Set<string>();
  const walk = (nodes: Node[]) =>
    nodes.forEach((n) => {
      paths.add(n.path);
      walk(n.children);
    });
  walk(tree);
  return paths;
}

async function categoryPaths(): Promise<Set<string> | undefined> {
  if (cache && cache.expiresAt > Date.now()) return cache.paths;

  inflight ??= loadCategoryPaths()
    .then((paths) => {
      cache = { paths, expiresAt: Date.now() + TTL_MS };
      return paths;
    })
    .finally(() => (inflight = undefined));

  try {
    return await inflight;
  } catch {
    return cache?.paths; // API fora: usa o último valor conhecido, mesmo vencido
  }
}

export async function proxy(request: NextRequest) {
  const path = decodeURIComponent(request.nextUrl.pathname).replace(/^\/+|\/+$/g, "");
  if (!path) return NextResponse.next();

  const categories = await categoryPaths();
  if (!categories) return NextResponse.next();

  const url = request.nextUrl.clone();

  if (categories.has(path)) {
    url.pathname = `/categoria/${path}`;
    return NextResponse.rewrite(url);
  }

  const slash = path.lastIndexOf("/");
  if (slash > 0 && categories.has(path.slice(0, slash))) {
    url.pathname = `/produto/${path}`;
    return NextResponse.rewrite(url);
  }

  return NextResponse.next();
}

export const config = {
  // Ignora rotas internas, arquivos e páginas com prefixo próprio.
  matcher: [
    "/((?!api/|files/|_next/|categoria/|produto/|colecoes/|status|conta|carrinho|checkout|entrar|cadastro|sitemap\\.xml|robots\\.txt|favicon\\.ico)(?!.*\\.).+)",
  ],
};
