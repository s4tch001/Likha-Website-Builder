import { create } from "zustand";
import { createElement } from "../model/elementFactory";
import type { BreakpointDef, ElementNode, Page, Project } from "../model/types";
import {
  isSafeCssPropertyName,
  isSafeCssValue,
} from "../model/projectValidation";

export const MIN_ZOOM = 10;
export const MAX_ZOOM = 400;

export const DEFAULT_CANVAS_BACKGROUND = "#141417";
const CANVAS_BG_STORAGE_KEY = "wb.canvasBackground";

function loadCanvasBackground(): string {
  if (typeof window === "undefined") {
    return DEFAULT_CANVAS_BACKGROUND;
  }

  try {
    return (
      window.localStorage.getItem(CANVAS_BG_STORAGE_KEY) ??
      DEFAULT_CANVAS_BACKGROUND
    );
  } catch {
    return DEFAULT_CANVAS_BACKGROUND;
  }
}

/** Alignment / distribution operations over a multi-selection. */
export type AlignMode =
  | "left"
  | "hcenter"
  | "right"
  | "top"
  | "vmiddle"
  | "bottom"
  | "distH"
  | "distV";

/** Depth-first search for a node by id across all pages of a project. */
export function findNode(project: Project, id: string): ElementNode | null {
  const walk = (node: ElementNode): ElementNode | null => {
    if (node.id === id) {
      return node;
    }
    for (const child of node.children) {
      const found = walk(child);
      if (found) {
        return found;
      }
    }
    return null;
  };

  for (const page of project.pages) {
    const found = walk(page.root);
    if (found) {
      return found;
    }
  }
  return null;
}

/** Finds the parent of the node with the given id, or null if it is a root / not found. */
export function findParent(project: Project, id: string): ElementNode | null {
  const walk = (node: ElementNode): ElementNode | null => {
    for (const child of node.children) {
      if (child.id === id) {
        return node;
      }
      const found = walk(child);
      if (found) {
        return found;
      }
    }
    return null;
  };

  for (const page of project.pages) {
    const found = walk(page.root);
    if (found) {
      return found;
    }
  }
  return null;
}

/**
 * Absolute position of a node within its page frame. The page root sits at the
 * frame origin (its own x/y are ignored); each descendant adds its local x/y.
 */
export function getAbsolutePosition(
  project: Project,
  id: string,
): { x: number; y: number } | null {
  const walk = (
    node: ElementNode,
    baseX: number,
    baseY: number,
    isRoot: boolean,
  ): { x: number; y: number } | null => {
    const ax = isRoot ? 0 : baseX + node.x;
    const ay = isRoot ? 0 : baseY + node.y;
    if (node.id === id) {
      return { x: ax, y: ay };
    }
    for (const child of node.children) {
      const found = walk(child, ax, ay, false);
      if (found) {
        return found;
      }
    }
    return null;
  };

  for (const page of project.pages) {
    const found = walk(page.root, 0, 0, true);
    if (found) {
      return found;
    }
  }
  return null;
}

/** Absolute world rectangle of an element. */
export interface ElementRect {
  id: string;
  x: number;
  y: number;
  w: number;
  h: number;
}

/**
 * Absolute rectangles for every non-root element of a page, used for marquee
 * hit-testing. The root is excluded (it fills the frame).
 */
export function collectElementRects(
  project: Project,
  pageId: string | null,
): ElementRect[] {
  const page = project.pages.find((p) => p.id === pageId) ?? project.pages[0];
  if (!page) {
    return [];
  }

  const rects: ElementRect[] = [];
  const walk = (node: ElementNode, baseX: number, baseY: number) => {
    for (const child of node.children) {
      const x = baseX + child.x;
      const y = baseY + child.y;
      rects.push({ id: child.id, x, y, w: child.width, h: child.height });
      walk(child, x, y);
    }
  };
  walk(page.root, 0, 0);
  return rects;
}

/** Generates a fresh element id for a duplicated node. */
function freshId(type: string): string {
  const suffix =
    typeof crypto.randomUUID === "function"
      ? crypto.randomUUID().slice(0, 8)
      : Math.random().toString(36).slice(2, 10);
  return `${type.toLowerCase()}-${suffix}`;
}

/** Recursively assigns new ids to a node and all its descendants (in place). */
function reassignIds(node: ElementNode): void {
  node.id = freshId(node.type);
  for (const child of node.children) {
    reassignIds(child);
  }
}

/** True if candidateId is the node itself or one of its descendants. */
function isSelfOrDescendant(
  project: Project,
  nodeId: string,
  candidateId: string,
): boolean {
  const node = findNode(project, nodeId);
  if (!node) {
    return false;
  }
  const walk = (n: ElementNode): boolean =>
    n.id === candidateId || n.children.some(walk);
  return walk(node);
}

interface EditorState {
  /** True once the editor shell has mounted and handshaken with the host. */
  ready: boolean;

  /** The loaded project (single source of truth), or null before first load. */
  project: Project | null;
  /** Id of the page currently shown on the canvas. */
  activePageId: string | null;
  /** Id of the active responsive breakpoint. */
  breakpointId: string | null;
  /** Ids of the currently selected elements (empty = nothing selected). */
  selectedIds: string[];

  /** Viewport zoom as a percentage (100 = 1:1). */
  zoom: number;
  /** Viewport pan offset in screen pixels. */
  panX: number;
  panY: number;

  /** Background colour of the canvas surface behind the page (persisted). */
  canvasBackground: string;

  /**
   * Bumped on every editor-originated model mutation (not on host-pushed loads).
   * The host connector watches this to push the updated project back to the host.
   */
  revision: number;

  /** Last authoritative host revision this editor snapshot is based on. */
  hostRevision: number;

  setReady: (ready: boolean) => void;
  setProject: (project: Project, hostRevision?: number) => void;
  acknowledgeHostRevision: (hostRevision: number) => void;
  setActivePage: (pageId: string) => void;
  setBreakpoint: (breakpointId: string) => void;
  setZoom: (zoom: number) => void;
  setView: (view: { zoom?: number; panX?: number; panY?: number }) => void;
  /** Sets (and persists) the canvas surface background colour. */
  setCanvasBackground: (color: string) => void;

  /** Adds a new element under the given parent (defaults to the active page root). */
  addElement: (node: ElementNode, parentId?: string) => void;
  /** Creates an element of the given type and adds it under the active page root. */
  insertElement: (type: string, x: number, y: number) => void;

  /** Replaces the selection with a single element (or clears it with null). */
  selectElement: (id: string | null) => void;
  /** Adds or removes an element from the current selection. */
  toggleSelect: (id: string) => void;
  /** Replaces (or, when additive, extends) the selection with several ids. */
  selectMany: (ids: string[], additive?: boolean) => void;
  /** Removes an element (and its subtree); cannot remove a page root. */
  deleteElement: (id: string) => void;
  /** Removes every selected element. */
  deleteSelection: () => void;
  /** Deep-clones an element next to itself with fresh ids and selects the copy. */
  duplicateElement: (id: string) => void;
  /** Duplicates every selected element, selecting the copies. */
  duplicateSelection: () => void;
  /** Sets a node's local position within its current parent. */
  moveElement: (id: string, x: number, y: number) => void;
  /** Offsets every selected element by (dx, dy) in one mutation. */
  moveSelectionBy: (dx: number, dy: number) => void;
  /** Aligns or distributes the current multi-selection. */
  alignSelection: (mode: AlignMode) => void;
  /** Sets a node's full geometry (position + size) within its current parent. */
  resizeElement: (
    id: string,
    x: number,
    y: number,
    width: number,
    height: number,
  ) => void;
  /** Sets a node's rotation in degrees (around its center). */
  rotateElement: (id: string, degrees: number) => void;
  /** Sets or clears a single CSS style on a node (empty value clears it). */
  setStyle: (id: string, name: string, value: string) => void;
  /** Sets a node's inline text content (empty clears it). */
  setText: (id: string, text: string) => void;
  /** Updates any provided geometry fields of a node, keeping the rest. */
  setGeometry: (
    id: string,
    geometry: { x?: number; y?: number; width?: number; height?: number },
  ) => void;
  /** Moves a node under a new parent, with coordinates local to that parent. */
  reparentElement: (
    id: string,
    newParentId: string,
    x: number,
    y: number,
  ) => void;
  /** Sets a node's editor-only hidden flag (hidden nodes are not rendered). */
  setHidden: (id: string, hidden: boolean) => void;
  /** Sets a node's editor-only locked flag (locked nodes resist canvas editing). */
  setLocked: (id: string, locked: boolean) => void;
  /** Sets a node's author-facing name (empty clears it → falls back to type·id). */
  renameElement: (id: string, name: string) => void;
  /** Moves a node to a new parent at a specific child index (Layers drag-reorder). */
  reorderElement: (id: string, newParentId: string, index: number) => void;
  /** Wraps the current selection in a new group container, preserving positions. */
  groupSelection: () => void;
  /** Replaces a group/container with its children, lifting them to its parent. */
  ungroupElement: (id: string) => void;
}

/** Id of a project's first/active page root, or null. */
function activeRootId(state: {
  project: Project | null;
  activePageId: string | null;
}): string | null {
  if (!state.project) {
    return null;
  }
  const page =
    state.project.pages.find((p) => p.id === state.activePageId) ??
    state.project.pages[0];
  return page ? page.root.id : null;
}

function clampZoom(zoom: number): number {
  return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
}

export const useEditorStore = create<EditorState>((set) => ({
  ready: false,
  project: null,
  activePageId: null,
  breakpointId: null,
  selectedIds: [],
  zoom: 100,
  panX: 80,
  panY: 80,
  canvasBackground: loadCanvasBackground(),
  revision: 0,
  hostRevision: 0,

  setReady: (ready) => set({ ready }),

  setProject: (project, hostRevision = 0) =>
    set((state) => {
      const firstPage: Page | undefined = project.pages[0];
      const base: BreakpointDef | undefined =
        project.breakpoints.find((b) => b.isBase) ?? project.breakpoints[0];
      const activePage = project.pages.some(
        (page) => page.id === state.activePageId,
      )
        ? state.activePageId
        : (firstPage?.id ?? null);
      const activeBreakpoint = project.breakpoints.some(
        (bp) => bp.id === state.breakpointId,
      )
        ? state.breakpointId
        : (base?.id ?? null);
      return {
        project,
        hostRevision,
        activePageId: activePage,
        breakpointId: activeBreakpoint,
        selectedIds: state.selectedIds.filter(
          (id) => findNode(project, id) !== null,
        ),
      };
    }),

  acknowledgeHostRevision: (hostRevision) => set({ hostRevision }),

  setActivePage: (pageId) => set({ activePageId: pageId }),
  setBreakpoint: (breakpointId) => set({ breakpointId }),
  setCanvasBackground: (color) =>
    set(() => {
      if (typeof window !== "undefined") {
        try {
          window.localStorage.setItem(CANVAS_BG_STORAGE_KEY, color);
        } catch {
          // Persistence is best-effort.
        }
      }
      return { canvasBackground: color };
    }),
  setZoom: (zoom) => set({ zoom: clampZoom(zoom) }),
  setView: (view) =>
    set((state) => ({
      zoom: view.zoom !== undefined ? clampZoom(view.zoom) : state.zoom,
      panX: view.panX !== undefined ? view.panX : state.panX,
      panY: view.panY !== undefined ? view.panY : state.panY,
    })),

  addElement: (node, parentId) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const targetId = parentId ?? activeRootId(state);
      if (!targetId) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const parent = findNode(project, targetId);
      if (!parent) {
        return {};
      }
      parent.children.push(node);
      return { project, revision: state.revision + 1 };
    }),

  insertElement: (type, x, y) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const targetId = activeRootId(state);
      if (!targetId) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const parent = findNode(project, targetId);
      if (!parent) {
        return {};
      }
      parent.children.push(createElement(type, x, y));
      return { project, revision: state.revision + 1 };
    }),

  selectElement: (id) => set({ selectedIds: id ? [id] : [] }),

  toggleSelect: (id) =>
    set((state) => {
      const exists = state.selectedIds.includes(id);
      return {
        selectedIds: exists
          ? state.selectedIds.filter((s) => s !== id)
          : [...state.selectedIds, id],
      };
    }),

  selectMany: (ids, additive = false) =>
    set((state) => {
      if (!additive) {
        return { selectedIds: [...ids] };
      }
      const merged = new Set(state.selectedIds);
      for (const id of ids) {
        merged.add(id);
      }
      return { selectedIds: [...merged] };
    }),

  deleteElement: (id) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (node?.locked) {
        return {}; // locked elements cannot be deleted
      }
      const parent = findParent(project, id);
      if (!parent) {
        return {}; // root or not found
      }
      parent.children = parent.children.filter((c) => c.id !== id);
      return {
        project,
        revision: state.revision + 1,
        selectedIds: state.selectedIds.filter((s) => s !== id),
      };
    }),

  deleteSelection: () =>
    set((state) => {
      if (!state.project || state.selectedIds.length === 0) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      for (const id of state.selectedIds) {
        const node = findNode(project, id);
        if (node?.locked) {
          continue; // locked elements cannot be deleted
        }
        const parent = findParent(project, id);
        if (parent) {
          parent.children = parent.children.filter((c) => c.id !== id);
        }
      }
      // Keep any locked (undeleted) elements selected; drop the rest.
      return {
        project,
        revision: state.revision + 1,
        selectedIds: state.selectedIds.filter(
          (id) => findNode(project, id) !== null,
        ),
      };
    }),

  duplicateElement: (id) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      const parent = findParent(project, id);
      if (!node || !parent) {
        return {}; // root or not found
      }

      const clone = structuredClone(node) as ElementNode;
      reassignIds(clone);
      clone.x = node.x + 16;
      clone.y = node.y + 16;
      if (clone.name) {
        clone.name = `${clone.name} copy`;
      }

      const index = parent.children.findIndex((c) => c.id === id);
      parent.children.splice(index + 1, 0, clone);
      return { project, revision: state.revision + 1, selectedIds: [clone.id] };
    }),

  duplicateSelection: () =>
    set((state) => {
      if (!state.project || state.selectedIds.length === 0) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const cloneIds: string[] = [];
      for (const id of state.selectedIds) {
        const node = findNode(project, id);
        const parent = findParent(project, id);
        if (!node || !parent) {
          continue;
        }
        const clone = structuredClone(node) as ElementNode;
        reassignIds(clone);
        clone.x = node.x + 16;
        clone.y = node.y + 16;
        if (clone.name) {
          clone.name = `${clone.name} copy`;
        }
        const index = parent.children.findIndex((c) => c.id === id);
        parent.children.splice(index + 1, 0, clone);
        cloneIds.push(clone.id);
      }
      if (cloneIds.length === 0) {
        return {};
      }
      return { project, revision: state.revision + 1, selectedIds: cloneIds };
    }),

  moveElement: (id, x, y) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }
      node.x = Math.round(x);
      node.y = Math.round(y);
      return { project, revision: state.revision + 1 };
    }),

  moveSelectionBy: (dx, dy) =>
    set((state) => {
      if (
        !state.project ||
        state.selectedIds.length === 0 ||
        (dx === 0 && dy === 0)
      ) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      for (const id of state.selectedIds) {
        const node = findNode(project, id);
        if (node && !node.locked) {
          node.x = Math.round(node.x + dx);
          node.y = Math.round(node.y + dy);
        }
      }
      return { project, revision: state.revision + 1 };
    }),

  alignSelection: (mode) =>
    set((state) => {
      if (!state.project || state.selectedIds.length < 2) {
        return {};
      }
      const project = structuredClone(state.project) as Project;

      const items = state.selectedIds
        .map((id) => {
          const node = findNode(project, id);
          const abs = getAbsolutePosition(project, id);
          // Locked elements are excluded — they neither move nor anchor the layout.
          return node && abs && !node.locked
            ? { node, absX: abs.x, absY: abs.y, w: node.width, h: node.height }
            : null;
        })
        .filter((i): i is NonNullable<typeof i> => i !== null);

      if (items.length < 2) {
        return {};
      }

      const minL = Math.min(...items.map((i) => i.absX));
      const maxR = Math.max(...items.map((i) => i.absX + i.w));
      const minT = Math.min(...items.map((i) => i.absY));
      const maxB = Math.max(...items.map((i) => i.absY + i.h));
      const cx = (minL + maxR) / 2;
      const cy = (minT + maxB) / 2;

      // A node's absolute position equals parentAbs + local, so shifting the
      // absolute edge by a delta means shifting the local coordinate by the same.
      for (const it of items) {
        switch (mode) {
          case "left":
            it.node.x += minL - it.absX;
            break;
          case "right":
            it.node.x += maxR - (it.absX + it.w);
            break;
          case "hcenter":
            it.node.x += cx - (it.absX + it.w / 2);
            break;
          case "top":
            it.node.y += minT - it.absY;
            break;
          case "bottom":
            it.node.y += maxB - (it.absY + it.h);
            break;
          case "vmiddle":
            it.node.y += cy - (it.absY + it.h / 2);
            break;
          default:
            break;
        }
      }

      if (mode === "distH" && items.length > 2) {
        const sorted = [...items].sort((a, b) => a.absX - b.absX);
        const span =
          sorted[sorted.length - 1].absX +
          sorted[sorted.length - 1].w -
          sorted[0].absX;
        const totalW = sorted.reduce((s, i) => s + i.w, 0);
        const gap = (span - totalW) / (sorted.length - 1);
        let cursor = sorted[0].absX;
        for (const it of sorted) {
          it.node.x += cursor - it.absX;
          cursor += it.w + gap;
        }
      } else if (mode === "distV" && items.length > 2) {
        const sorted = [...items].sort((a, b) => a.absY - b.absY);
        const span =
          sorted[sorted.length - 1].absY +
          sorted[sorted.length - 1].h -
          sorted[0].absY;
        const totalH = sorted.reduce((s, i) => s + i.h, 0);
        const gap = (span - totalH) / (sorted.length - 1);
        let cursor = sorted[0].absY;
        for (const it of sorted) {
          it.node.y += cursor - it.absY;
          cursor += it.h + gap;
        }
      }

      for (const it of items) {
        it.node.x = Math.round(it.node.x);
        it.node.y = Math.round(it.node.y);
      }

      return { project, revision: state.revision + 1 };
    }),

  resizeElement: (id, x, y, width, height) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }
      node.x = Math.round(x);
      node.y = Math.round(y);
      node.width = Math.round(Math.max(8, width));
      node.height = Math.round(Math.max(8, height));
      return { project, revision: state.revision + 1 };
    }),

  rotateElement: (id, degrees) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }
      // Normalize to (-180, 180] and round to the nearest degree.
      let deg = Math.round(degrees) % 360;
      if (deg > 180) {
        deg -= 360;
      }
      if (deg <= -180) {
        deg += 360;
      }
      node.rotation = deg;
      return { project, revision: state.revision + 1 };
    }),

  setStyle: (id, name, value) =>
    set((state) => {
      if (
        !state.project ||
        !isSafeCssPropertyName(name) ||
        (value !== "" && !isSafeCssValue(value))
      ) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }

      // At a non-base breakpoint, edits write to that breakpoint's override layer;
      // at the base they write the base styles. Clearing a value removes it from
      // whichever layer is active (reverting to the inherited/base value).
      const activeBp = state.project.breakpoints.find(
        (b) => b.id === state.breakpointId,
      );
      if (!activeBp || activeBp.isBase) {
        if (value === "") {
          delete node.styles[name];
        } else {
          node.styles[name] = value;
        }
      } else {
        if (!node.responsiveStyles) {
          node.responsiveStyles = {};
        }
        const layer = node.responsiveStyles[activeBp.id] ?? {};
        if (value === "") {
          delete layer[name];
        } else {
          layer[name] = value;
        }
        if (Object.keys(layer).length === 0) {
          delete node.responsiveStyles[activeBp.id];
        } else {
          node.responsiveStyles[activeBp.id] = layer;
        }
      }
      return { project, revision: state.revision + 1 };
    }),

  setText: (id, text) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }
      node.text = text === "" ? undefined : text;
      return { project, revision: state.revision + 1 };
    }),

  setGeometry: (id, geometry) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }
      if (geometry.x != null) {
        node.x = Math.round(geometry.x);
      }
      if (geometry.y != null) {
        node.y = Math.round(geometry.y);
      }
      if (geometry.width != null) {
        node.width = Math.round(Math.max(1, geometry.width));
      }
      if (geometry.height != null) {
        node.height = Math.round(Math.max(1, geometry.height));
      }
      return { project, revision: state.revision + 1 };
    }),

  reparentElement: (id, newParentId, x, y) =>
    set((state) => {
      if (!state.project || id === newParentId) {
        return {};
      }
      // Never move a node into itself or one of its own descendants.
      if (isSelfOrDescendant(state.project, id, newParentId)) {
        return {};
      }

      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      const oldParent = findParent(project, id);
      const newParent = findNode(project, newParentId);
      if (!node || node.locked || !oldParent || !newParent) {
        return {};
      }

      oldParent.children = oldParent.children.filter((c) => c.id !== id);
      node.x = Math.round(x);
      node.y = Math.round(y);
      newParent.children.push(node);
      return { project, revision: state.revision + 1 };
    }),

  setHidden: (id, hidden) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.hidden === hidden) {
        return {};
      }
      node.hidden = hidden;
      // A hidden element can't be interacted with, so drop it from the selection.
      return {
        project,
        revision: state.revision + 1,
        selectedIds: hidden
          ? state.selectedIds.filter((s) => s !== id)
          : state.selectedIds,
      };
    }),

  setLocked: (id, locked) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked === locked) {
        return {};
      }
      node.locked = locked;
      return { project, revision: state.revision + 1 };
    }),

  renameElement: (id, name) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }
      const trimmed = name.trim();
      node.name = trimmed === "" ? undefined : trimmed;
      return { project, revision: state.revision + 1 };
    }),

  reorderElement: (id, newParentId, index) =>
    set((state) => {
      if (!state.project || id === newParentId) {
        return {};
      }
      // Never move a node into itself or one of its own descendants.
      if (isSelfOrDescendant(state.project, id, newParentId)) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const node = findNode(project, id);
      if (!node || node.locked) {
        return {};
      }
      const oldParent = findParent(project, id);
      const newParent = findNode(project, newParentId);
      if (!oldParent || !newParent) {
        return {}; // moving a page root, or target not found
      }
      const oldIndex = oldParent.children.findIndex((c) => c.id === id);
      oldParent.children.splice(oldIndex, 1);
      // Removing an earlier sibling in the same parent shifts the target index down.
      let insertIndex = index;
      if (oldParent === newParent && oldIndex < index) {
        insertIndex -= 1;
      }
      insertIndex = Math.max(
        0,
        Math.min(insertIndex, newParent.children.length),
      );
      newParent.children.splice(insertIndex, 0, node);
      return { project, revision: state.revision + 1, selectedIds: [id] };
    }),

  groupSelection: () =>
    set((state) => {
      if (!state.project || state.selectedIds.length < 2) {
        return {};
      }
      const src = state.project;
      // Keep only top-level picks (drop any whose ancestor is also selected).
      const ids = state.selectedIds.filter(
        (id) =>
          !state.selectedIds.some(
            (other) => other !== id && isSelfOrDescendant(src, other, id),
          ),
      );
      if (ids.length < 2) {
        return {};
      }

      const project = structuredClone(src) as Project;
      // Absolute rects, captured before any structural change. Locked members are
      // excluded (grouping would reposition them).
      const members = ids
        .map((id) => {
          const node = findNode(project, id);
          const abs = getAbsolutePosition(project, id);
          return node && abs && !node.locked
            ? { node, absX: abs.x, absY: abs.y }
            : null;
        })
        .filter((m): m is NonNullable<typeof m> => m !== null);
      if (members.length < 2) {
        return {};
      }

      const minX = Math.min(...members.map((m) => m.absX));
      const minY = Math.min(...members.map((m) => m.absY));
      const maxX = Math.max(...members.map((m) => m.absX + m.node.width));
      const maxY = Math.max(...members.map((m) => m.absY + m.node.height));

      const firstParent = findParent(project, members[0].node.id);
      if (!firstParent) {
        return {};
      }
      const parentAbs = getAbsolutePosition(project, firstParent.id) ?? {
        x: 0,
        y: 0,
      };
      const insertIndex = firstParent.children.findIndex(
        (c) => c.id === members[0].node.id,
      );

      const group: ElementNode = {
        id: freshId("group"),
        type: "Div",
        name: "Group",
        x: Math.round(minX - parentAbs.x),
        y: Math.round(minY - parentAbs.y),
        width: Math.round(maxX - minX),
        height: Math.round(maxY - minY),
        rotation: 0,
        attributes: {},
        styles: {},
        responsiveStyles: {},
        hidden: false,
        locked: false,
        children: [],
      };

      // Detach each member and re-home it under the group, preserving its position.
      for (const m of members) {
        const oldParent = findParent(project, m.node.id);
        if (oldParent) {
          oldParent.children = oldParent.children.filter(
            (c) => c.id !== m.node.id,
          );
        }
        m.node.x = Math.round(m.absX - minX);
        m.node.y = Math.round(m.absY - minY);
        group.children.push(m.node);
      }

      const idx =
        insertIndex >= 0
          ? Math.min(insertIndex, firstParent.children.length)
          : firstParent.children.length;
      firstParent.children.splice(idx, 0, group);

      return { project, revision: state.revision + 1, selectedIds: [group.id] };
    }),

  ungroupElement: (id) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const project = structuredClone(state.project) as Project;
      const group = findNode(project, id);
      const parent = findParent(project, id);
      if (!group || group.locked || !parent || group.children.length === 0) {
        return {};
      }
      const groupIndex = parent.children.findIndex((c) => c.id === id);
      // Lift children into the group's parent, converting to the parent's space.
      const lifted = group.children.map((child) => {
        child.x = Math.round(group.x + child.x);
        child.y = Math.round(group.y + child.y);
        return child;
      });
      parent.children.splice(groupIndex, 1, ...lifted);
      return {
        project,
        revision: state.revision + 1,
        selectedIds: lifted.map((c) => c.id),
      };
    }),
}));

/** Selector: the page currently being edited. */
export function useActivePage(): Page | null {
  return useEditorStore((state) => {
    if (!state.project) {
      return null;
    }
    return (
      state.project.pages.find((p) => p.id === state.activePageId) ??
      state.project.pages[0] ??
      null
    );
  });
}

/** Selector: the active breakpoint definition. */
export function useActiveBreakpoint(): BreakpointDef | undefined {
  return useEditorStore((state) => {
    if (!state.project) {
      return undefined;
    }
    return (
      state.project.breakpoints.find((b) => b.id === state.breakpointId) ??
      state.project.breakpoints.find((b) => b.isBase) ??
      state.project.breakpoints[0]
    );
  });
}
