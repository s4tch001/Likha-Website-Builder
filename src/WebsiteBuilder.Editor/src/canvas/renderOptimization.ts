import type { BreakpointDef, ElementNode, ProjectAsset } from "../model/types";
import { ASSET_VIRTUAL_ORIGIN } from "../model/assetElements";

function recordsEqual(
  left: Readonly<Record<string, string>>,
  right: Readonly<Record<string, string>>,
): boolean {
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  return (
    leftKeys.length === rightKeys.length &&
    leftKeys.every((key) => left[key] === right[key])
  );
}

function responsiveStylesEqual(
  left: Readonly<Record<string, Record<string, string>>>,
  right: Readonly<Record<string, Record<string, string>>>,
): boolean {
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  return (
    leftKeys.length === rightKeys.length &&
    leftKeys.every((key) => {
      const leftLayer = left[key];
      const rightLayer = right[key];
      return rightLayer !== undefined && recordsEqual(leftLayer, rightLayer);
    })
  );
}

/**
 * Compares the render-owned part of two nodes. Child references deliberately
 * remain significant: a changed descendant must reopen its ancestor path, while
 * unchanged leaves can skip React reconciliation even before structural sharing.
 */
export function elementRenderEqual(
  left: Readonly<ElementNode>,
  right: Readonly<ElementNode>,
): boolean {
  if (left === right) return true;
  if (
    left.id !== right.id ||
    left.type !== right.type ||
    left.name !== right.name ||
    left.x !== right.x ||
    left.y !== right.y ||
    left.width !== right.width ||
    left.height !== right.height ||
    left.rotation !== right.rotation ||
    left.text !== right.text ||
    left.hidden !== right.hidden ||
    left.locked !== right.locked ||
    !recordsEqual(left.attributes, right.attributes) ||
    !recordsEqual(left.styles, right.styles) ||
    !responsiveStylesEqual(left.responsiveStyles, right.responsiveStyles) ||
    left.children.length !== right.children.length
  ) {
    return false;
  }
  return left.children.every((child, index) => child === right.children[index]);
}

export function breakpointsEqual(
  left: readonly BreakpointDef[],
  right: readonly BreakpointDef[],
): boolean {
  return (
    left.length === right.length &&
    left.every((item, index) => {
      const candidate = right[index];
      return (
        candidate !== undefined &&
        item.id === candidate.id &&
        item.label === candidate.label &&
        item.maxWidth === candidate.maxWidth &&
        item.isBase === candidate.isBase
      );
    })
  );
}

export function assetsEqual(
  left: readonly ProjectAsset[],
  right: readonly ProjectAsset[],
): boolean {
  return (
    left.length === right.length &&
    left.every((item, index) => {
      const candidate = right[index];
      return (
        candidate !== undefined &&
        item.id === candidate.id &&
        item.relativePath === candidate.relativePath &&
        item.storedFileName === candidate.storedFileName
      );
    })
  );
}

/** Builds a URL map exclusively from canonical managed-asset metadata. */
export function createEditorAssetUrlMap(
  assets: readonly ProjectAsset[],
): ReadonlyMap<string, string> {
  const urls = new Map<string, string>();
  for (const asset of assets) {
    if (
      asset.relativePath === `Assets/${asset.storedFileName}` &&
      !asset.storedFileName.includes("/") &&
      !asset.storedFileName.includes("\\")
    ) {
      urls.set(
        asset.relativePath,
        `${ASSET_VIRTUAL_ORIGIN}/${encodeURIComponent(asset.storedFileName)}`,
      );
    }
  }
  return urls;
}
