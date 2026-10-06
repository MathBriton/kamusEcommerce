import type { MetadataRoute } from "next";
import { getCollections, getSitemapEntries } from "@/lib/catalog";
import { siteUrl } from "@/lib/env";

// Gerado sob demanda (os dados ficam 1h em cache), para não depender da API no build.
export const dynamic = "force-dynamic";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const home: MetadataRoute.Sitemap = [{ url: siteUrl, changeFrequency: "daily", priority: 1 }];

  try {
    const [entries, collections] = await Promise.all([getSitemapEntries(), getCollections()]);
    return [
      ...home,
      ...collections.map((c) => ({
        url: `${siteUrl}/colecoes/${c.slug}`,
        changeFrequency: "daily" as const,
      })),
      ...entries.map((e) => ({
        url: `${siteUrl}/${e.path}`,
        lastModified: e.updatedAt,
        // categorias têm poucos segmentos; produtos ficam abaixo delas
        priority: e.path.split("/").length <= 2 ? 0.8 : 0.6,
      })),
    ];
  } catch {
    return home;
  }
}
