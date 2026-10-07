import path from "node:path";
import type { NextConfig } from "next";

// Raiz do monorepo: permite importar packages/tokens e mantém o rastreamento do build
// standalone consistente (turbopack.root e outputFileTracingRoot precisam ser iguais).
const repoRoot = path.resolve(__dirname, "../..");

const nextConfig: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  turbopack: { root: repoRoot },
  outputFileTracingRoot: repoRoot,
};

export default nextConfig;
