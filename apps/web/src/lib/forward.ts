import { apiUrl } from "./env";

const HOP_BY_HOP = [
  "connection",
  "keep-alive",
  "transfer-encoding",
  "upgrade",
  "host",
  "content-length",
];

/**
 * Encaminha a requisição do navegador para a API .NET (padrão backend-for-frontend).
 * O navegador fala só com o Next.js (mesma origem); cookies vão e voltam intactos.
 */
export async function forwardToApi(request: Request, targetPath: string): Promise<Response> {
  const source = new URL(request.url);
  const headers = new Headers(request.headers);
  for (const h of HOP_BY_HOP) headers.delete(h);

  // A API usa estes cabeçalhos para saber o protocolo original (cookies Secure em produção).
  headers.set("x-forwarded-proto", source.protocol.replace(":", ""));
  headers.set("x-forwarded-host", source.host);

  const hasBody = !["GET", "HEAD"].includes(request.method);
  const upstream = await fetch(`${apiUrl}${targetPath}${source.search}`, {
    method: request.method,
    headers,
    body: hasBody ? await request.arrayBuffer() : undefined,
    redirect: "manual",
    cache: "no-store",
  });

  const responseHeaders = new Headers(upstream.headers);
  for (const h of HOP_BY_HOP) responseHeaders.delete(h);
  responseHeaders.delete("content-encoding");

  return new Response(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers: responseHeaders,
  });
}
