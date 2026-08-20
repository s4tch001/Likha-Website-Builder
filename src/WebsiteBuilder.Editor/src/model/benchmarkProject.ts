import type { ElementNode, Project } from "./types";

export const MAX_BENCHMARK_ELEMENTS = 10_000;

/** Deterministic flat project used only by the opt-in `?benchmark=N` performance harness. */
export function createBenchmarkProject(requestedCount: number): Project {
  const safeCount = Number.isFinite(requestedCount) ? requestedCount : 0;
  const count = Math.min(
    MAX_BENCHMARK_ELEMENTS,
    Math.max(0, Math.trunc(safeCount)),
  );
  const children: ElementNode[] = Array.from({ length: count }, (_, index) => ({
    id: `bench-${index}`,
    type: "Div",
    name: `Benchmark ${index}`,
    x: (index % 100) * 90,
    y: Math.floor(index / 100) * 50,
    width: 80,
    height: 40,
    rotation: 0,
    attributes: {},
    styles: { background: index % 2 === 0 ? "#1e3a8a" : "#334155" },
    responsiveStyles: {},
    hidden: false,
    locked: false,
    children: [],
  }));

  return {
    schemaVersion: 2,
    id: `benchmark-${count}`,
    name: `Benchmark · ${count.toLocaleString()} elements`,
    createdUtc: "2026-08-20T00:00:00Z",
    modifiedUtc: "2026-08-20T00:00:00Z",
    breakpoints: [
      { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
      { id: "mobile", label: "Mobile", maxWidth: 480, isBase: false },
    ],
    variables: {},
    assets: [],
    pages: [
      {
        id: "benchmark-page",
        name: "Benchmark",
        route: "index",
        root: {
          id: "benchmark-root",
          type: "Section",
          name: "Benchmark Root",
          x: 0,
          y: 0,
          width: 0,
          height: Math.max(1_000, Math.ceil(count / 100) * 50),
          rotation: 0,
          attributes: {},
          styles: { background: "#0f172a" },
          responsiveStyles: {},
          hidden: false,
          locked: false,
          children,
        },
      },
    ],
  };
}
