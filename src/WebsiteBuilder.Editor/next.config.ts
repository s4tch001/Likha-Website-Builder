import type { NextConfig } from "next";

const nextConfig = {
  output: "export",
  poweredByHeader: false,
  experimental: {
    // TypeScript 7 is the native Go compiler and intentionally has no legacy
    // JavaScript compiler API. Make Next invoke the project-local `tsc` CLI.
    useTypeScriptCli: true,
  },
  images: {
    unoptimized: true,
  },
} satisfies NextConfig;

export default nextConfig;
