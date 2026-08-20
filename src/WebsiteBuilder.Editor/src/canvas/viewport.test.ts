import { describe, expect, it } from "vitest";
import { createElement } from "../model/elementFactory";
import { cullElementTree } from "./viewport";

describe("cullElementTree", () => {
  it("keeps visible nodes and prunes distant siblings", () => {
    const root = createElement("Section", 0, 0);
    const visible = createElement("Div", 20, 20);
    const distant = createElement("Div", 5_000, 5_000);
    root.children = [visible, distant];

    const result = cullElementTree(root, {
      x: 0,
      y: 0,
      width: 500,
      height: 500,
    });
    expect(result.children.map((node) => node.id)).toEqual([visible.id]);
    expect(result.children[0]).toBe(visible);
  });

  it("keeps the ancestor path to preserved descendants", () => {
    const root = createElement("Section", 0, 0);
    const parent = createElement("Div", 2_000, 2_000);
    const child = createElement("Div", 10, 10);
    parent.children = [child];
    root.children = [parent];

    const result = cullElementTree(
      root,
      { x: 0, y: 0, width: 100, height: 100 },
      { preserveIds: new Set([child.id]) },
    );
    expect(result.children[0]?.id).toBe(parent.id);
    expect(result.children[0]?.children[0]).toBe(child);
  });

  it("retains a complete forced subtree for live dragging", () => {
    const root = createElement("Section", 0, 0);
    const group = createElement("Div", 2_000, 2_000);
    group.children = [createElement("Div", 10_000, 10_000)];
    root.children = [group];

    const result = cullElementTree(
      root,
      { x: 0, y: 0, width: 100, height: 100 },
      { forceSubtreeIds: new Set([group.id]) },
    );
    expect(result.children[0]).toBe(group);
  });
});
