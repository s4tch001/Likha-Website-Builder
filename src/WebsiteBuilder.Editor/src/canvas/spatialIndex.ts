import type { ElementNode, Project } from "../model/types";
import type { ElementRect } from "../store/editorStore";
import type { WorldRect } from "./viewport";
import { rectsIntersect } from "./viewport";

const CELL_SIZE = 256;
const MAX_CELLS_PER_RECT = 4_096;
const projectIndexCache = new WeakMap<ElementNode, ElementSpatialIndex>();

interface AxisTarget {
  id: string;
  value: number;
}

export interface ElementSpatialIndex {
  readonly rects: readonly ElementRect[];
  readonly cells: ReadonlyMap<string, readonly ElementRect[]>;
  readonly largeRects: readonly ElementRect[];
  readonly xTargets: readonly AxisTarget[];
  readonly yTargets: readonly AxisTarget[];
}

function cellKey(x: number, y: number): string {
  return `${x}:${y}`;
}

function collectRects(root: ElementNode): ElementRect[] {
  const rects: ElementRect[] = [];
  const walk = (node: ElementNode, baseX: number, baseY: number): void => {
    for (const child of node.children) {
      if (child.hidden) continue;
      const x = baseX + child.x;
      const y = baseY + child.y;
      rects.push({ id: child.id, x, y, w: child.width, h: child.height });
      walk(child, x, y);
    }
  };
  walk(root, 0, 0);
  return rects;
}

export function createElementSpatialIndex(
  root: ElementNode,
): ElementSpatialIndex {
  const rects = collectRects(root);
  const cells = new Map<string, ElementRect[]>();
  const largeRects: ElementRect[] = [];
  const xTargets: AxisTarget[] = [];
  const yTargets: AxisTarget[] = [];

  for (const rect of rects) {
    xTargets.push(
      { id: rect.id, value: rect.x },
      { id: rect.id, value: rect.x + rect.w / 2 },
      { id: rect.id, value: rect.x + rect.w },
    );
    yTargets.push(
      { id: rect.id, value: rect.y },
      { id: rect.id, value: rect.y + rect.h / 2 },
      { id: rect.id, value: rect.y + rect.h },
    );

    const minCellX = Math.floor(rect.x / CELL_SIZE);
    const maxCellX = Math.floor((rect.x + rect.w) / CELL_SIZE);
    const minCellY = Math.floor(rect.y / CELL_SIZE);
    const maxCellY = Math.floor((rect.y + rect.h) / CELL_SIZE);
    const cellCount = (maxCellX - minCellX + 1) * (maxCellY - minCellY + 1);
    if (!Number.isSafeInteger(cellCount) || cellCount > MAX_CELLS_PER_RECT) {
      largeRects.push(rect);
      continue;
    }
    for (let x = minCellX; x <= maxCellX; x += 1) {
      for (let y = minCellY; y <= maxCellY; y += 1) {
        const key = cellKey(x, y);
        const bucket = cells.get(key) ?? [];
        bucket.push(rect);
        cells.set(key, bucket);
      }
    }
  }
  xTargets.sort((left, right) => left.value - right.value);
  yTargets.sort((left, right) => left.value - right.value);
  return { rects, cells, largeRects, xTargets, yTargets };
}

export function getProjectSpatialIndex(
  project: Project,
  pageId: string | null,
): ElementSpatialIndex | null {
  const page =
    project.pages.find((item) => item.id === pageId) ?? project.pages[0];
  if (!page) return null;
  const cached = projectIndexCache.get(page.root);
  if (cached) return cached;
  const index = createElementSpatialIndex(page.root);
  projectIndexCache.set(page.root, index);
  return index;
}

export function queryElementRects(
  index: ElementSpatialIndex,
  area: WorldRect,
): ElementRect[] {
  const minCellX = Math.floor(area.x / CELL_SIZE);
  const maxCellX = Math.floor((area.x + area.width) / CELL_SIZE);
  const minCellY = Math.floor(area.y / CELL_SIZE);
  const maxCellY = Math.floor((area.y + area.height) / CELL_SIZE);
  const queryCellCount = (maxCellX - minCellX + 1) * (maxCellY - minCellY + 1);
  if (
    !Number.isSafeInteger(queryCellCount) ||
    queryCellCount > MAX_CELLS_PER_RECT
  ) {
    return index.rects.filter((rect) =>
      rectsIntersect(
        { x: rect.x, y: rect.y, width: rect.w, height: rect.h },
        area,
      ),
    );
  }

  const candidates = new Map<string, ElementRect>();
  for (let x = minCellX; x <= maxCellX; x += 1) {
    for (let y = minCellY; y <= maxCellY; y += 1) {
      for (const rect of index.cells.get(cellKey(x, y)) ?? []) {
        candidates.set(rect.id, rect);
      }
    }
  }
  for (const rect of index.largeRects) candidates.set(rect.id, rect);
  return [...candidates.values()].filter((rect) =>
    rectsIntersect(
      { x: rect.x, y: rect.y, width: rect.w, height: rect.h },
      area,
    ),
  );
}

function lowerBound(targets: readonly AxisTarget[], value: number): number {
  let low = 0;
  let high = targets.length;
  while (low < high) {
    const middle = (low + high) >>> 1;
    if (targets[middle].value < value) low = middle + 1;
    else high = middle;
  }
  return low;
}

export function queryAxisTargets(
  index: ElementSpatialIndex,
  axis: "x" | "y",
  candidates: readonly number[],
  threshold: number,
  exclude: ReadonlySet<string>,
): number[] {
  const targets = axis === "x" ? index.xTargets : index.yTargets;
  const values = new Set<number>();
  for (const candidate of candidates) {
    for (
      let position = lowerBound(targets, candidate - threshold);
      position < targets.length &&
      targets[position].value <= candidate + threshold;
      position += 1
    ) {
      const target = targets[position];
      if (!exclude.has(target.id)) values.add(target.value);
    }
  }
  return [...values];
}
