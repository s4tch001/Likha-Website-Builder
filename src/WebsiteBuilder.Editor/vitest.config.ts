import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    coverage: {
      provider: "v8",
      reporter: ["text", "json-summary", "lcov"],
      include: [
        "src/bridge/**/*.ts",
        "src/canvas/snap.ts",
        "src/host/**/*.ts",
        "src/model/**/*.ts",
        "src/store/**/*.ts",
      ],
      exclude: ["src/**/*.test.ts", "src/model/types.ts"],
      thresholds: {
        // Baseline ratchet: increase these as the remaining UI orchestration is
        // covered, but never allow new work to reduce today's verified floor.
        lines: 64,
        functions: 70,
        statements: 64,
        branches: 55,
      },
    },
  },
});
