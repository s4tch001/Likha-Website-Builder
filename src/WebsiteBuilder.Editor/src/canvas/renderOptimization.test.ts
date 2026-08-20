import { describe, expect, it } from "vitest";
import { createElement } from "../model/elementFactory";
import type { BreakpointDef, ProjectAsset } from "../model/types";
import {
  assetsEqual,
  breakpointsEqual,
  createEditorAssetUrlMap,
  elementRenderEqual,
} from "./renderOptimization";

describe("render optimization inputs", () => {
  it("skips equivalent cloned leaves but reopens a changed ancestor path", () => {
    const leaf = createElement("Div", 10, 20);
    const equivalent = structuredClone(leaf);
    expect(elementRenderEqual(leaf, equivalent)).toBe(true);

    equivalent.x += 1;
    expect(elementRenderEqual(leaf, equivalent)).toBe(false);

    const parent = createElement("Div", 0, 0);
    parent.children.push(leaf);
    const clonedParent = structuredClone(parent);
    expect(elementRenderEqual(parent, clonedParent)).toBe(false);
  });

  it("compares stable breakpoint and asset render metadata semantically", () => {
    const breakpoints: BreakpointDef[] = [
      { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
    ];
    expect(breakpointsEqual(breakpoints, structuredClone(breakpoints))).toBe(
      true,
    );

    const asset = {
      id: "asset-1",
      name: "Hero",
      storedFileName: "hero image.png",
      relativePath: "Assets/hero image.png",
      kind: "Image",
      mediaType: "image/png",
      sizeBytes: 1,
      sha256: "0".repeat(64),
      importedUtc: "2026-08-20T00:00:00Z",
    } satisfies ProjectAsset;
    expect(assetsEqual([asset], structuredClone([asset]))).toBe(true);
    expect(createEditorAssetUrlMap([asset]).get(asset.relativePath)).toBe(
      "https://project-assets.local/hero%20image.png",
    );
  });

  it("rejects non-canonical asset paths from the render URL map", () => {
    const asset = {
      id: "asset-1",
      name: "Bad",
      storedFileName: "safe.png",
      relativePath: "Assets/../safe.png",
      kind: "Image",
      mediaType: "image/png",
      sizeBytes: 1,
      sha256: "0".repeat(64),
      importedUtc: "2026-08-20T00:00:00Z",
    } satisfies ProjectAsset;
    expect(createEditorAssetUrlMap([asset]).size).toBe(0);
  });
});
