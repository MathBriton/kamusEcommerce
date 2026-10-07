import type { MetadataRoute } from "next";
import { siteUrl } from "@/lib/env";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      // Rotas internas (alvos de rewrite) e áreas privadas não devem ser indexadas.
      disallow: [
        "/api/",
        "/categoria/",
        "/produto/",
        "/conta",
        "/carrinho",
        "/checkout",
        "/status",
        "/design-system",
      ],
    },
    sitemap: `${siteUrl}/sitemap.xml`,
  };
}
