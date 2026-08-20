import type { ElementNode, Project } from "../model/types";
import { collectElementRects, findNode } from "../store/editorStore";

/** Screen pixels within which a moving edge snaps to a target. */
export const SNAP_THRESHOLD = 6;

/** Collects the id of a node and all of its descendants. */
export function subtreeIds(project: Project, id: string): Set<string> {
  const ids = new Set<string>();
  const node = findNode(project, id);
  const walk = (n: ElementNode) => {
    ids.add(n.id);
    n.children.forEach(walk);
  };
  if (node) {
    walk(node);
  }
  return ids;
}

/** The geometry of the element being dragged, plus the subtree to ignore as a target. */
export interface SnapInput {
  origAbsX: number;
  origAbsY: number;
  origW: number;
  origH: number;
  exclude: Set<string>;
}

export interface SnapResult {
  dx: number;
  dy: number;
  /** World x of the vertical guide, or null. */
  guideX: number | null;
  /** World y of the horizontal guide, or null. */
  guideY: number | null;
}

/**
 * Snaps the dragged element's edges/center to other elements' edges/centers and
 * the frame, within a zoom-aware threshold. Pure function of its inputs (no DOM),
 * so it is unit-testable. Returns the adjusted offset plus any guide lines.
 */
export function computeSnap(
  drag: SnapInput,
  dx: number,
  dy: number,
  project: Project,
  pageId: string | null,
  frameW: number,
  frameH: number,
  zoom: number,
): SnapResult {
  const z = zoom / 100;
  const threshold = SNAP_THRESHOLD / z;

  const w = drag.origW;
  const h = drag.origH;
  const left = drag.origAbsX + dx;
  const top = drag.origAbsY + dy;
  const xCandidates = [left, left + w / 2, left + w];
  const yCandidates = [top, top + h / 2, top + h];

  const xTargets = [0, frameW / 2, frameW];
  const yTargets = [0, frameH / 2, frameH];
  for (const r of collectElementRects(project, pageId)) {
    if (drag.exclude.has(r.id)) {
      continue;
    }
    xTargets.push(r.x, r.x + r.w / 2, r.x + r.w);
    yTargets.push(r.y, r.y + r.h / 2, r.y + r.h);
  }

  const best = (cands: number[], targets: number[]) => {
    let result: { diff: number; snap: number; line: number | null } = {
      diff: threshold,
      snap: 0,
      line: null,
    };
    for (const c of cands) {
      for (const t of targets) {
        const diff = Math.abs(c - t);
        if (diff < result.diff) {
          result = { diff, snap: t - c, line: t };
        }
      }
    }
    return result;
  };

  const bx = best(xCandidates, xTargets);
  const by = best(yCandidates, yTargets);

  return {
    dx: dx + bx.snap,
    dy: dy + by.snap,
    guideX: bx.line,
    guideY: by.line,
  };
}
