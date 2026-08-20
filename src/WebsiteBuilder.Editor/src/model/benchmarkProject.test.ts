import { describe, expect, it } from "vitest";
import {
  createBenchmarkProject,
  MAX_BENCHMARK_ELEMENTS,
} from "./benchmarkProject";
import { isValidProject } from "./projectValidation";

describe("benchmark project", () => {
  it("creates a deterministic valid flat project", () => {
    const project = createBenchmarkProject(250);
    expect(project.pages[0].root.children).toHaveLength(250);
    expect(project.pages[0].root.children[249].id).toBe("bench-249");
    expect(isValidProject(project)).toBe(true);
  });

  it("clamps unsafe requested sizes", () => {
    expect(createBenchmarkProject(-10).pages[0].root.children).toHaveLength(0);
    expect(
      createBenchmarkProject(Number.POSITIVE_INFINITY).pages[0].root.children,
    ).toHaveLength(0);
    expect(
      createBenchmarkProject(MAX_BENCHMARK_ELEMENTS + 1).pages[0].root.children,
    ).toHaveLength(MAX_BENCHMARK_ELEMENTS);
  });
});
