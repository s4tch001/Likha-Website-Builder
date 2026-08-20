import type { ElementNode } from "../model/types";

export interface WorldRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface CullOptions {
  /** Nodes that must remain mounted, together with the ancestor path to them. */
  preserveIds?: ReadonlySet<string>;
  /** Nodes whose complete subtree must remain mounted (for live group dragging). */
  forceSubtreeIds?: ReadonlySet<string>;
}

export function rectsIntersect(left: WorldRect, right: WorldRect): boolean {
  return (
    left.x < right.x + right.width &&
    left.x + left.width > right.x &&
    left.y < right.y + right.height &&
    left.y + left.height > right.y
  );
}

/**
 * Produces a structurally shared render tree containing only viewport-visible
 * nodes plus required interaction paths. Geometry remains local to the original
 * ancestors, so pruning does not alter layout or persisted project state.
 */
export function cullElementTree(
  root: ElementNode,
  viewport: WorldRect,
  options: CullOptions = {},
): ElementNode {
  const preserveIds = options.preserveIds ?? new Set<string>();
  const forceSubtreeIds = options.forceSubtreeIds ?? new Set<string>();

  const walk = (
    node: ElementNode,
    absoluteX: number,
    absoluteY: number,
    isRoot: boolean,
    forceSubtree: boolean,
  ): ElementNode | null => {
    if (node.hidden) return null;
    const nodeX = isRoot ? 0 : absoluteX + node.x;
    const nodeY = isRoot ? 0 : absoluteY + node.y;
    const forced = forceSubtree || forceSubtreeIds.has(node.id);
    if (forced) return node;

    const visibleSelf =
      isRoot ||
      preserveIds.has(node.id) ||
      rectsIntersect(
        { x: nodeX, y: nodeY, width: node.width, height: node.height },
        viewport,
      );

    const children: ElementNode[] = [];
    for (const child of node.children) {
      const visibleChild = walk(child, nodeX, nodeY, false, false);
      if (visibleChild) children.push(visibleChild);
    }

    if (!visibleSelf && children.length === 0) return null;
    if (
      children.length === node.children.length &&
      children.every((child, index) => child === node.children[index])
    ) {
      return node;
    }
    return { ...node, children };
  };

  return walk(root, 0, 0, true, false) ?? { ...root, children: [] };
}
