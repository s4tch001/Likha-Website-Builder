import { create } from "zustand";
import { createElement } from "../model/elementFactory";
import type {
  BreakpointDef,
  ElementNode,
  Page,
  Project,
  ProjectAsset,
} from "../model/types";
import { createAssetElement } from "../model/assetElements";
import {
  isSafeCssPropertyName,
  isSafeCssValue,
  isValidElementTree,
} from "../model/projectValidation";

export const MIN_ZOOM = 10;
export const MAX_ZOOM = 400;

export const DEFAULT_CANVAS_BACKGROUND = "#141417";
const CANVAS_BG_STORAGE_KEY = "wb.canvasBackground";
const HISTORY_LIMIT = 50;
const undoHistory: Project[] = [];
const redoHistory: Project[] = [];
let applyingHistory = false;
let elementClipboard: ElementNode[] = [];
let clipboardPasteCount = 0;

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

type NodeMutator = (node: ElementNode) => void;

function mutableNodeCopy(
  node: ElementNode,
  children: ElementNode[],
): ElementNode {
  return {
    ...node,
    attributes: { ...node.attributes },
    styles: { ...node.styles },
    responsiveStyles: Object.fromEntries(
      Object.entries(node.responsiveStyles).map(([id, layer]) => [
        id,
        { ...layer },
      ]),
    ),
    children,
  };
}

/**
 * Immutable path-copy update. Only matching nodes and their ancestor path are
 * cloned; untouched pages, branches, assets, and project metadata retain their
 * references for cheap rendering and structurally shared history.
 */
function updateProjectNodes(
  project: Project,
  ids: ReadonlySet<string>,
  mutate: NodeMutator,
): Project | null {
  if (ids.size === 0) return null;
  let matched = 0;

  const walk = (node: ElementNode): ElementNode => {
    let childrenChanged = false;
    const children = node.children.map((child) => {
      const updated = walk(child);
      if (updated !== child) childrenChanged = true;
      return updated;
    });
    if (!ids.has(node.id) && !childrenChanged) return node;

    const updated = mutableNodeCopy(
      node,
      childrenChanged ? children : [...node.children],
    );
    if (ids.has(node.id)) {
      matched += 1;
      mutate(updated);
    }
    return updated;
  };

  let pagesChanged = false;
  const pages = project.pages.map((page) => {
    const root = walk(page.root);
    if (root === page.root) return page;
    pagesChanged = true;
    return { ...page, root };
  });
  return matched > 0 && pagesChanged ? { ...project, pages } : null;
}

function updateProjectNode(
  project: Project,
  id: string,
  mutate: NodeMutator,
): Project | null {
  const walk = (node: ElementNode): { node: ElementNode; found: boolean } => {
    if (node.id === id) {
      const updated = mutableNodeCopy(node, [...node.children]);
      mutate(updated);
      return { node: updated, found: true };
    }
    for (let index = 0; index < node.children.length; index += 1) {
      const child = node.children[index];
      const result = walk(child);
      if (!result.found) continue;
      const children = [...node.children];
      children[index] = result.node;
      return { node: mutableNodeCopy(node, children), found: true };
    }
    return { node, found: false };
  };

  for (let pageIndex = 0; pageIndex < project.pages.length; pageIndex += 1) {
    const page = project.pages[pageIndex];
    const result = walk(page.root);
    if (!result.found) continue;
    const pages = [...project.pages];
    pages[pageIndex] = { ...page, root: result.node };
    return { ...project, pages };
  }
  return null;
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

  /** Bounded editor-side project history availability. */
  canUndo: boolean;
  canRedo: boolean;
  canPaste: boolean;

  setReady: (ready: boolean) => void;
  setProject: (project: Project, hostRevision?: number) => void;
  acknowledgeHostRevision: (hostRevision: number) => void;
  undo: () => void;
  redo: () => void;
  copySelection: () => void;
  cutSelection: () => void;
  pasteClipboard: () => void;
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
  /** Inserts an element only when the asset matches current canonical metadata. */
  insertAsset: (asset: ProjectAsset, x: number, y: number) => void;
  /** Inserts a validated reusable subtree with fresh project-wide ids. */
  insertComponent: (root: ElementNode, x: number, y: number) => void;

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
  canUndo: false,
  canRedo: false,
  canPaste: false,

  setReady: (ready) => set({ ready }),

  setProject: (project, hostRevision = 0) =>
    set((state) => {
      elementClipboard = [];
      clipboardPasteCount = 0;
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
        canPaste: false,
      };
    }),

  acknowledgeHostRevision: (hostRevision) => set({ hostRevision }),
  undo: () => applyHistory("undo"),
  redo: () => applyHistory("redo"),
  copySelection: () =>
    set((state) => {
      if (!state.project || state.selectedIds.length === 0) return {};
      const topLevelIds = state.selectedIds.filter(
        (id) =>
          !state.selectedIds.some(
            (other) =>
              other !== id && isSelfOrDescendant(state.project!, other, id),
          ),
      );
      elementClipboard = topLevelIds
        .map((id) => findNode(state.project!, id))
        .filter((node): node is ElementNode => node !== null)
        .map((node) => structuredClone(node) as ElementNode);
      clipboardPasteCount = 0;
      return { canPaste: elementClipboard.length > 0 };
    }),
  cutSelection: () => {
    const state = useEditorStore.getState();
    state.copySelection();
    useEditorStore.getState().deleteSelection();
  },
  pasteClipboard: () =>
    set((state) => {
      if (!state.project || elementClipboard.length === 0) return {};
      const targetId = activeRootId(state);
      if (!targetId) return {};
      clipboardPasteCount += 1;
      const offset = clipboardPasteCount * 16;
      const pasted = elementClipboard.map((source) => {
        const node = structuredClone(source) as ElementNode;
        reassignIds(node);
        node.x = Math.round(node.x + offset);
        node.y = Math.round(node.y + offset);
        return node;
      });
      const project = updateProjectNode(state.project, targetId, (parent) => {
        parent.children.push(...pasted);
      });
      if (!project) return {};
      return {
        project,
        revision: state.revision + 1,
        selectedIds: pasted.map((node) => node.id),
      };
    }),

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
      const project = updateProjectNode(state.project, targetId, (parent) => {
        parent.children.push(node);
      });
      if (!project) return {};
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
      const project = updateProjectNode(state.project, targetId, (parent) => {
        parent.children.push(createElement(type, x, y));
      });
      if (!project) return {};
      return { project, revision: state.revision + 1 };
    }),

  insertAsset: (asset, x, y) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const canonical = state.project.assets.find(
        (candidate) =>
          candidate.id === asset.id &&
          candidate.storedFileName === asset.storedFileName &&
          candidate.relativePath === asset.relativePath &&
          candidate.kind === asset.kind &&
          candidate.mediaType === asset.mediaType,
      );
      const targetId = activeRootId(state);
      if (!canonical || !targetId) {
        return {};
      }
      const node = createAssetElement(canonical, x, y);
      if (!node) {
        return {};
      }
      const project = updateProjectNode(state.project, targetId, (parent) => {
        parent.children.push(node);
      });
      if (!project) return {};
      return { project, revision: state.revision + 1, selectedIds: [node.id] };
    }),

  insertComponent: (root, x, y) =>
    set((state) => {
      if (!state.project || !isValidElementTree(root)) {
        return {};
      }
      const targetId = activeRootId(state);
      if (!targetId) {
        return {};
      }
      const component = structuredClone(root) as ElementNode;
      reassignIds(component);
      component.x = Math.round(Math.max(0, x));
      component.y = Math.round(Math.max(0, y));
      const project = updateProjectNode(state.project, targetId, (parent) => {
        parent.children.push(component);
      });
      if (!project) return {};
      return {
        project,
        revision: state.revision + 1,
        selectedIds: [component.id],
      };
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
      const node = findNode(state.project, id);
      if (node?.locked) {
        return {}; // locked elements cannot be deleted
      }
      const parent = findParent(state.project, id);
      if (!parent) {
        return {}; // root or not found
      }
      const project = updateProjectNode(state.project, parent.id, (copy) => {
        copy.children = copy.children.filter((child) => child.id !== id);
      });
      if (!project) return {};
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
      const deletable = new Set(
        state.selectedIds.filter((id) => {
          const node = findNode(state.project!, id);
          return (
            node !== null && !node.locked && findParent(state.project!, id)
          );
        }),
      );
      if (deletable.size === 0) return {};
      const parentIds = new Set<string>();
      for (const id of deletable) {
        const parent = findParent(state.project, id);
        if (parent) parentIds.add(parent.id);
      }
      const project = updateProjectNodes(state.project, parentIds, (parent) => {
        parent.children = parent.children.filter(
          (child) => !deletable.has(child.id),
        );
      });
      if (!project) return {};
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
      const node = findNode(state.project, id);
      const parent = findParent(state.project, id);
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

      const project = updateProjectNode(state.project, parent.id, (copy) => {
        const index = copy.children.findIndex((child) => child.id === id);
        copy.children.splice(index + 1, 0, clone);
      });
      if (!project) return {};
      return { project, revision: state.revision + 1, selectedIds: [clone.id] };
    }),

  duplicateSelection: () =>
    set((state) => {
      if (!state.project || state.selectedIds.length === 0) {
        return {};
      }
      const cloneIds: string[] = [];
      const operations = new Map<
        string,
        { sourceId: string; clone: ElementNode }[]
      >();
      for (const id of state.selectedIds) {
        const node = findNode(state.project, id);
        const parent = findParent(state.project, id);
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
        const parentOperations = operations.get(parent.id) ?? [];
        parentOperations.push({ sourceId: id, clone });
        operations.set(parent.id, parentOperations);
        cloneIds.push(clone.id);
      }
      if (cloneIds.length === 0) {
        return {};
      }
      const project = updateProjectNodes(
        state.project,
        new Set(operations.keys()),
        (parent) => {
          for (const operation of operations.get(parent.id) ?? []) {
            const index = parent.children.findIndex(
              (child) => child.id === operation.sourceId,
            );
            if (index >= 0) {
              parent.children.splice(index + 1, 0, operation.clone);
            }
          }
        },
      );
      if (!project) return {};
      return { project, revision: state.revision + 1, selectedIds: cloneIds };
    }),

  moveElement: (id, x, y) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const node = findNode(state.project, id);
      if (!node || node.locked) {
        return {};
      }
      const project = updateProjectNode(state.project, id, (copy) => {
        copy.x = Math.round(x);
        copy.y = Math.round(y);
      });
      if (!project) return {};
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
      const movableIds = new Set(
        state.selectedIds.filter((id) => {
          const node = findNode(state.project!, id);
          return node !== null && !node.locked;
        }),
      );
      const mutate = (node: ElementNode) => {
        node.x = Math.round(node.x + dx);
        node.y = Math.round(node.y + dy);
      };
      const [singleId] = movableIds;
      const project =
        movableIds.size === 1 && singleId
          ? updateProjectNode(state.project, singleId, mutate)
          : updateProjectNodes(state.project, movableIds, mutate);
      if (!project) return {};
      return { project, revision: state.revision + 1 };
    }),

  alignSelection: (mode) =>
    set((state) => {
      if (!state.project || state.selectedIds.length < 2) {
        return {};
      }
      const items = state.selectedIds
        .map((id) => {
          const node = findNode(state.project!, id);
          const abs = getAbsolutePosition(state.project!, id);
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
      const positions = new Map<string, { x: number; y: number }>();
      for (const it of items) {
        let x = it.node.x;
        let y = it.node.y;
        switch (mode) {
          case "left":
            x += minL - it.absX;
            break;
          case "right":
            x += maxR - (it.absX + it.w);
            break;
          case "hcenter":
            x += cx - (it.absX + it.w / 2);
            break;
          case "top":
            y += minT - it.absY;
            break;
          case "bottom":
            y += maxB - (it.absY + it.h);
            break;
          case "vmiddle":
            y += cy - (it.absY + it.h / 2);
            break;
          default:
            break;
        }
        positions.set(it.node.id, { x, y });
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
          positions.set(it.node.id, {
            x: it.node.x + cursor - it.absX,
            y: it.node.y,
          });
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
          positions.set(it.node.id, {
            x: it.node.x,
            y: it.node.y + cursor - it.absY,
          });
          cursor += it.h + gap;
        }
      }

      const project = updateProjectNodes(
        state.project,
        new Set(positions.keys()),
        (node) => {
          const position = positions.get(node.id);
          if (position) {
            node.x = Math.round(position.x);
            node.y = Math.round(position.y);
          }
        },
      );
      if (!project) return {};

      return { project, revision: state.revision + 1 };
    }),

  resizeElement: (id, x, y, width, height) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const node = findNode(state.project, id);
      if (!node || node.locked) {
        return {};
      }
      const project = updateProjectNode(state.project, id, (copy) => {
        copy.x = Math.round(x);
        copy.y = Math.round(y);
        copy.width = Math.round(Math.max(8, width));
        copy.height = Math.round(Math.max(8, height));
      });
      if (!project) return {};
      return { project, revision: state.revision + 1 };
    }),

  rotateElement: (id, degrees) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const node = findNode(state.project, id);
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
      const project = updateProjectNode(state.project, id, (copy) => {
        copy.rotation = deg;
      });
      if (!project) return {};
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
      const node = findNode(state.project, id);
      if (!node || node.locked) {
        return {};
      }

      // At a non-base breakpoint, edits write to that breakpoint's override layer;
      // at the base they write the base styles. Clearing a value removes it from
      // whichever layer is active (reverting to the inherited/base value).
      const activeBp = state.project.breakpoints.find(
        (b) => b.id === state.breakpointId,
      );
      const project = updateProjectNode(state.project, id, (copy) => {
        if (!activeBp || activeBp.isBase) {
          if (value === "") {
            delete copy.styles[name];
          } else {
            copy.styles[name] = value;
          }
        } else {
          const layer = copy.responsiveStyles[activeBp.id] ?? {};
          if (value === "") {
            delete layer[name];
          } else {
            layer[name] = value;
          }
          if (Object.keys(layer).length === 0) {
            delete copy.responsiveStyles[activeBp.id];
          } else {
            copy.responsiveStyles[activeBp.id] = layer;
          }
        }
      });
      if (!project) return {};
      return { project, revision: state.revision + 1 };
    }),

  setText: (id, text) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const node = findNode(state.project, id);
      if (!node || node.locked) {
        return {};
      }
      const project = updateProjectNode(state.project, id, (copy) => {
        copy.text = text === "" ? undefined : text;
      });
      if (!project) return {};
      return { project, revision: state.revision + 1 };
    }),

  setGeometry: (id, geometry) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const node = findNode(state.project, id);
      if (!node || node.locked) {
        return {};
      }
      const project = updateProjectNode(state.project, id, (copy) => {
        if (geometry.x != null) copy.x = Math.round(geometry.x);
        if (geometry.y != null) copy.y = Math.round(geometry.y);
        if (geometry.width != null) {
          copy.width = Math.round(Math.max(1, geometry.width));
        }
        if (geometry.height != null) {
          copy.height = Math.round(Math.max(1, geometry.height));
        }
      });
      if (!project) return {};
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

      const node = findNode(state.project, id);
      const oldParent = findParent(state.project, id);
      const newParent = findNode(state.project, newParentId);
      if (!node || node.locked || !oldParent || !newParent) {
        return {};
      }
      const movedNode = mutableNodeCopy(node, [...node.children]);
      movedNode.x = Math.round(x);
      movedNode.y = Math.round(y);
      const project = updateProjectNodes(
        state.project,
        new Set([oldParent.id, newParent.id]),
        (parent) => {
          if (parent.id === oldParent.id) {
            parent.children = parent.children.filter(
              (child) => child.id !== id,
            );
          }
          if (parent.id === newParent.id) parent.children.push(movedNode);
        },
      );
      if (!project) return {};
      return { project, revision: state.revision + 1 };
    }),

  setHidden: (id, hidden) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const node = findNode(state.project, id);
      if (!node || node.hidden === hidden) {
        return {};
      }
      const project = updateProjectNode(state.project, id, (copy) => {
        copy.hidden = hidden;
      });
      if (!project) return {};
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
      const node = findNode(state.project, id);
      if (!node || node.locked === locked) {
        return {};
      }
      const project = updateProjectNode(state.project, id, (copy) => {
        copy.locked = locked;
      });
      if (!project) return {};
      return { project, revision: state.revision + 1 };
    }),

  renameElement: (id, name) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const node = findNode(state.project, id);
      if (!node || node.locked) {
        return {};
      }
      const trimmed = name.trim();
      const project = updateProjectNode(state.project, id, (copy) => {
        copy.name = trimmed === "" ? undefined : trimmed;
      });
      if (!project) return {};
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
      const node = findNode(state.project, id);
      if (!node || node.locked) {
        return {};
      }
      const oldParent = findParent(state.project, id);
      const newParent = findNode(state.project, newParentId);
      if (!oldParent || !newParent) {
        return {}; // moving a page root, or target not found
      }
      const oldIndex = oldParent.children.findIndex((c) => c.id === id);
      // Removing an earlier sibling in the same parent shifts the target index down.
      let insertIndex = index;
      if (oldParent === newParent && oldIndex < index) {
        insertIndex -= 1;
      }
      const project = updateProjectNodes(
        state.project,
        new Set([oldParent.id, newParent.id]),
        (parent) => {
          if (parent.id === oldParent.id) {
            parent.children = parent.children.filter(
              (child) => child.id !== id,
            );
          }
          if (parent.id === newParent.id) {
            const boundedIndex = Math.max(
              0,
              Math.min(insertIndex, parent.children.length),
            );
            parent.children.splice(boundedIndex, 0, node);
          }
        },
      );
      if (!project) return {};
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

      // Absolute rects, captured before any structural change. Locked members are
      // excluded (grouping would reposition them).
      const members = ids
        .map((id) => {
          const node = findNode(src, id);
          const abs = getAbsolutePosition(src, id);
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

      const firstParent = findParent(src, members[0].node.id);
      if (!firstParent) {
        return {};
      }
      const parentAbs = getAbsolutePosition(src, firstParent.id) ?? {
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
      const memberIds = new Set(members.map((member) => member.node.id));
      const parentIds = new Set<string>([firstParent.id]);
      for (const member of members) {
        const oldParent = findParent(src, member.node.id);
        if (oldParent) parentIds.add(oldParent.id);
        const groupedNode = mutableNodeCopy(member.node, [
          ...member.node.children,
        ]);
        groupedNode.x = Math.round(member.absX - minX);
        groupedNode.y = Math.round(member.absY - minY);
        group.children.push(groupedNode);
      }

      const project = updateProjectNodes(src, parentIds, (parent) => {
        parent.children = parent.children.filter(
          (child) => !memberIds.has(child.id),
        );
        if (parent.id === firstParent.id) {
          const idx =
            insertIndex >= 0
              ? Math.min(insertIndex, parent.children.length)
              : parent.children.length;
          parent.children.splice(idx, 0, group);
        }
      });
      if (!project) return {};

      return { project, revision: state.revision + 1, selectedIds: [group.id] };
    }),

  ungroupElement: (id) =>
    set((state) => {
      if (!state.project) {
        return {};
      }
      const group = findNode(state.project, id);
      const parent = findParent(state.project, id);
      if (!group || group.locked || !parent || group.children.length === 0) {
        return {};
      }
      const groupIndex = parent.children.findIndex((c) => c.id === id);
      // Lift children into the group's parent, converting to the parent's space.
      const lifted = group.children.map((child) => {
        const copy = mutableNodeCopy(child, [...child.children]);
        copy.x = Math.round(group.x + child.x);
        copy.y = Math.round(group.y + child.y);
        return copy;
      });
      const project = updateProjectNode(state.project, parent.id, (copy) => {
        copy.children.splice(groupIndex, 1, ...lifted);
      });
      if (!project) return {};
      return {
        project,
        revision: state.revision + 1,
        selectedIds: lifted.map((c) => c.id),
      };
    }),
}));

function refreshHistoryAvailability(): void {
  const canUndo = undoHistory.length > 0;
  const canRedo = redoHistory.length > 0;
  const current = useEditorStore.getState();
  if (current.canUndo !== canUndo || current.canRedo !== canRedo) {
    useEditorStore.setState({ canUndo, canRedo });
  }
}

function clearEditorHistory(): void {
  undoHistory.length = 0;
  redoHistory.length = 0;
  refreshHistoryAvailability();
}

function applyHistory(direction: "undo" | "redo"): void {
  const source = direction === "undo" ? undoHistory : redoHistory;
  const destination = direction === "undo" ? redoHistory : undoHistory;
  const snapshot = source.pop();
  const current = useEditorStore.getState();
  if (!snapshot || !current.project) {
    refreshHistoryAvailability();
    return;
  }

  destination.push(current.project);
  applyingHistory = true;
  try {
    const project = snapshot;
    useEditorStore.setState({
      project,
      revision: current.revision + 1,
      selectedIds: current.selectedIds.filter(
        (id) => findNode(project, id) !== null,
      ),
    });
  } finally {
    applyingHistory = false;
  }
  refreshHistoryAvailability();
}

useEditorStore.subscribe((state, previous) => {
  if (applyingHistory || state.project === previous.project) {
    return;
  }

  if (state.revision === previous.revision + 1 && previous.project !== null) {
    undoHistory.push(previous.project);
    if (undoHistory.length > HISTORY_LIMIT) {
      undoHistory.shift();
    }
    redoHistory.length = 0;
    refreshHistoryAvailability();
    return;
  }

  // Host loads/conflict recovery replace the canonical project without an
  // editor revision increment. Old snapshots must never cross that boundary.
  clearEditorHistory();
});

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
