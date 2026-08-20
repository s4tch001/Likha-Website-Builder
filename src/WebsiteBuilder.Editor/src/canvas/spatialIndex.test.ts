import { describe, expect, it } from "vitest";
import { createElement } from "../model/elementFactory";
import {
  createElementSpatialIndex,
  queryAxisTargets,
  queryElementRects,
} from "./spatialIndex";

describe("element spatial index", () => {
  it("returns only rectangles intersecting the requested area", () => {
    const root = createElement("Section", 0, 0);
    const near = createElement("Div", 10, 10);
    const far = createElement("Div", 5_000, 5_000);
    root.children = [near, far];
    const index = createElementSpatialIndex(root);

    expect(
      queryElementRects(index, { x: 0, y: 0, width: 500, height: 500 }).map(
        (rect) => rect.id,
      ),
    ).toEqual([near.id]);
  });

  it("finds nearby guide coordinates while excluding dragged ids", () => {
    const root = createElement("Section", 0, 0);
    const first = createElement("Div", 100, 100);
    const excluded = createElement("Div", 102, 100);
    root.children = [first, excluded];
    const index = createElementSpatialIndex(root);

    expect(
      queryAxisTargets(index, "x", [104], 6, new Set([excluded.id])),
    ).toContain(100);
    expect(queryAxisTargets(index, "x", [104], 1, new Set())).toEqual([]);
  });
});
