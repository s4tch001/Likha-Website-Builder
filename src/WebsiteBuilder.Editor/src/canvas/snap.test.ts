import { describe, expect, it } from "vitest";
import type { ElementNode, Project } from "../model/types";
import { computeSnap, subtreeIds } from "./snap";

function node(
  id: string,
  x: number,
  y: number,
  w: number,
  h: number,
  children: ElementNode[] = [],
): ElementNode {
  return {
    id,
    type: "Div",
    x,
    y,
    width: w,
    height: h,
    rotation: 0,
    attributes: {},
    styles: {},
    responsiveStyles: {},
    hidden: false,
    locked: false,
    children,
  };
}

function project(children: ElementNode[]): Project {
  const root = node("root", 0, 0, 0, 0, children);
  return {
    schemaVersion: 2,
    id: "p",
    name: "Test",
    createdUtc: "",
    modifiedUtc: "",
    breakpoints: [
      { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
    ],
    pages: [{ id: "page1", name: "Home", route: "index", root }],
    variables: {},
    assets: [],
  };
}

describe("computeSnap", () => {
  it("snaps a left edge to another element's left edge within threshold", () => {
    // 'a' left = 100; dragging 'b' (currently x=140) toward 100 by raw dx that lands at 104.
    const proj = project([
      node("a", 100, 0, 50, 50),
      node("b", 140, 0, 50, 50),
    ]);
    const drag = {
      origAbsX: 140,
      origAbsY: 0,
      origW: 50,
      origH: 50,
      exclude: subtreeIds(proj, "b"),
    };

    // raw dx = -36 → moving left edge to 104, which is within 6px of 100 → snaps to 100.
    const result = computeSnap(drag, -36, 0, proj, "page1", 1440, 1000, 100);

    expect(drag.origAbsX + result.dx).toBe(100); // snapped left edge
    expect(result.guideX).toBe(100);
  });

  it("does not snap when outside the threshold", () => {
    // The other element is far away in both axes, so no edge/center lines up.
    const proj = project([
      node("a", 600, 600, 50, 50),
      node("b", 140, 300, 50, 50),
    ]);
    const drag = {
      origAbsX: 140,
      origAbsY: 300,
      origW: 50,
      origH: 50,
      exclude: subtreeIds(proj, "b"),
    };

    // raw dx = -20 → edges at 120/145/170, all > 6px from any target → no snap.
    const result = computeSnap(drag, -20, 0, proj, "page1", 1440, 1000, 100);

    expect(result.dx).toBe(-20);
    expect(result.guideX).toBeNull();
  });

  it("excludes the dragged element's own subtree from snap targets", () => {
    const child = node("child", 10, 10, 20, 20);
    const proj = project([node("parent", 200, 200, 100, 100, [child])]);
    const drag = {
      origAbsX: 200,
      origAbsY: 200,
      origW: 100,
      origH: 100,
      exclude: subtreeIds(proj, "parent"),
    };

    // No other elements and far from frame edges → must not snap to its own child.
    const result = computeSnap(drag, 3, 3, proj, "page1", 1440, 1000, 100);

    expect(result.guideX).toBeNull();
    expect(result.guideY).toBeNull();
  });
});
