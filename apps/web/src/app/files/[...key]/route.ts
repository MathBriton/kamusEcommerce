import { forwardToApi } from "@/lib/forward";

export async function GET(request: Request, { params }: RouteContext<"/files/[...key]">) {
  const { key } = await params;
  return forwardToApi(request, `/files/${key.map(encodeURIComponent).join("/")}`);
}
