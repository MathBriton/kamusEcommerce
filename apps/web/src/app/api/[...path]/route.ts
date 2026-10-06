import { forwardToApi } from "@/lib/forward";

type Context = RouteContext<"/api/[...path]">;

async function handler(request: Request, { params }: Context) {
  const { path } = await params;
  return forwardToApi(request, `/api/${path.map(encodeURIComponent).join("/")}`);
}

export { handler as GET, handler as POST, handler as PUT, handler as PATCH, handler as DELETE };
