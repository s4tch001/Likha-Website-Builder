import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  frameWidthFor,
  type BreakpointDef,
  type ProjectAsset,
} from "../model/types";
import { DRAG_MIME } from "../palette/catalog";
import { ASSET_DRAG_MIME, editorFontFaceCss } from "../model/assetElements";
import {
  COMPONENT_DRAG_MIME,
  parseComponentDragPayload,
} from "../model/componentDrag";
import {
  getAbsolutePosition,
  findNode,
  findParent,
  MAX_ZOOM,
  MIN_ZOOM,
  useActiveBreakpoint,
  useActivePage,
  useEditorStore,
} from "../store/editorStore";
import { computeSnap, subtreeIds } from "./snap";
import { CanvasRenderContext } from "./CanvasContext";
import ElementRenderer from "./ElementRenderer";
import Ruler from "./Rulers";
import SelectionOverlay from "./SelectionOverlay";
import { useElementSize } from "./useElementSize";
import { createEditorAssetUrlMap } from "./renderOptimization";
import { getProjectSpatialIndex, queryElementRects } from "./spatialIndex";
import { cullElementTree, type WorldRect } from "./viewport";

const RULER_SIZE = 24;
const FRAME_MIN_HEIGHT = 1000;
const DRAG_THRESHOLD = 3; // px before a press becomes a move
const VIRTUALIZATION_OVERSCAN_PX = 400;
const INITIAL_VIEWPORT_WIDTH = 1920;
const INITIAL_VIEWPORT_HEIGHT = 1080;

/** Element types that may contain children (valid drop targets for nesting). */
const CONTAINER_TYPES = new Set([
  "Section",
  "Container",
  "Div",
  "Card",
  "Navbar",
  "Sidebar",
  "Footer",
  "Form",
]);

function clamp(zoom: number): number {
  return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
}

function renderedElement(id: string): HTMLElement | null {
  return document.getElementById(`wb-element-${id}`);
}

interface ElementDrag {
  id: string;
  startClientX: number;
  startClientY: number;
  origParentId: string;
  origLocalX: number;
  origLocalY: number;
  origAbsX: number;
  origAbsY: number;
  origW: number;
  origH: number;
  moved: boolean;
  /** Ids moved together (single = [id]); group drags never re-parent. */
  ids: string[];
  group: boolean;
  /** The dragged subtree (excluded from snap targets). */
  exclude: Set<string>;
}

interface CanvasContextMenuState {
  left: number;
  top: number;
}

/**
 * The infinite design canvas. Renders the active page from the store and adds:
 *  - ctrl+wheel zoom-to-cursor, wheel/space-drag/middle-mouse panning;
 *  - click-to-select elements;
 *  - drag-to-move existing elements, re-parenting into containers with a live
 *    drop-target highlight.
 */
export default function Canvas() {
  const page = useActivePage();
  const breakpoint = useActiveBreakpoint();

  const zoom = useEditorStore((s) => s.zoom);
  const panX = useEditorStore((s) => s.panX);
  const panY = useEditorStore((s) => s.panY);
  const selectedIds = useEditorStore((s) => s.selectedIds);
  const breakpointId = useEditorStore((s) => s.breakpointId);
  const breakpoints = useEditorStore((s) => s.project?.breakpoints);
  const project = useEditorStore((s) => s.project);
  const canvasBackground = useEditorStore((s) => s.canvasBackground);
  const setView = useEditorStore((s) => s.setView);
  const insertElement = useEditorStore((s) => s.insertElement);
  const insertAsset = useEditorStore((s) => s.insertAsset);
  const insertComponent = useEditorStore((s) => s.insertComponent);

  // `structuredClone` currently replaces these arrays on every mutation. Keep
  // the context value stable while their small, render-relevant metadata is
  // unchanged so memoized descendants are not invalidated through Context.
  const breakpointSignature = JSON.stringify(breakpoints ?? []);
  const stableBreakpoints = useMemo(
    () => JSON.parse(breakpointSignature) as BreakpointDef[],
    [breakpointSignature],
  );
  const assetSignature = JSON.stringify(project?.assets ?? []);
  const assetUrls = useMemo(
    () => createEditorAssetUrlMap(JSON.parse(assetSignature) as ProjectAsset[]),
    [assetSignature],
  );
  const renderContext = useMemo(
    () => ({
      breakpoints: stableBreakpoints,
      breakpointId,
      assetUrls,
    }),
    [assetUrls, breakpointId, stableBreakpoints],
  );

  const clipRef = useRef<HTMLDivElement>(null);
  const { width, height } = useElementSize(clipRef);

  const [spaceDown, setSpaceDown] = useState(false);
  const [isPanning, setIsPanning] = useState(false);
  const panState = useRef<{
    startX: number;
    startY: number;
    panX: number;
    panY: number;
  } | null>(null);

  const elementDrag = useRef<ElementDrag | null>(null);
  const [dragIds, setDragIds] = useState<string[]>([]);
  const [dragOffset, setDragOffset] = useState({ dx: 0, dy: 0 });
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);
  const [contextMenu, setContextMenu] = useState<CanvasContextMenuState | null>(
    null,
  );

  // Marquee (rubber-band) selection, tracked in screen coords relative to the clip.
  const marquee = useRef<{
    startX: number;
    startY: number;
    additive: boolean;
  } | null>(null);
  const [marqueeRect, setMarqueeRect] = useState<{
    left: number;
    top: number;
    width: number;
    height: number;
  } | null>(null);

  // Smart-guide lines (screen coords within the clip), shown while snapping.
  const [guides, setGuides] = useState<{ x: number | null; y: number | null }>({
    x: null,
    y: null,
  });

  const renderZoom = zoom / 100;
  const virtualViewport = useMemo<WorldRect>(() => {
    const overscan = VIRTUALIZATION_OVERSCAN_PX / renderZoom;
    const viewportWidth = width > 0 ? width : INITIAL_VIEWPORT_WIDTH;
    const viewportHeight = height > 0 ? height : INITIAL_VIEWPORT_HEIGHT;
    return {
      x: -panX / renderZoom - overscan,
      y: -panY / renderZoom - overscan,
      width: viewportWidth / renderZoom + overscan * 2,
      height: viewportHeight / renderZoom + overscan * 2,
    };
  }, [height, panX, panY, renderZoom, width]);
  const visibleRoot = useMemo(() => {
    if (!page) return null;
    return cullElementTree(page.root, virtualViewport, {
      preserveIds: new Set(
        dropTargetId
          ? [...selectedIds, ...dragIds, dropTargetId]
          : [...selectedIds, ...dragIds],
      ),
      forceSubtreeIds: new Set(dragIds),
    });
  }, [dragIds, dropTargetId, page, selectedIds, virtualViewport]);

  const previousSelectedIds = useRef<readonly string[]>([]);
  useLayoutEffect(() => {
    const next = new Set(selectedIds);
    for (const id of previousSelectedIds.current) {
      if (!next.has(id)) renderedElement(id)?.classList.remove("selected");
    }
    for (const id of selectedIds) {
      renderedElement(id)?.classList.add("selected");
    }
    previousSelectedIds.current = selectedIds;
  }, [selectedIds, visibleRoot]);

  const previousDropTargetId = useRef<string | null>(null);
  useLayoutEffect(() => {
    if (previousDropTargetId.current !== dropTargetId) {
      if (previousDropTargetId.current) {
        renderedElement(previousDropTargetId.current)?.classList.remove(
          "drop-target",
        );
      }
      if (dropTargetId) {
        renderedElement(dropTargetId)?.classList.add("drop-target");
      }
      previousDropTargetId.current = dropTargetId;
    }
  }, [dropTargetId, visibleRoot]);

  const previousDragIds = useRef<readonly string[]>([]);
  useLayoutEffect(() => {
    const next = new Set(dragIds);
    for (const id of previousDragIds.current) {
      if (next.has(id)) continue;
      const element = renderedElement(id);
      if (element) {
        element.classList.remove("dragging");
        const state = useEditorStore.getState();
        const node = state.project ? findNode(state.project, id) : null;
        element.style.transform = node?.rotation
          ? `rotate(${node.rotation}deg)`
          : "";
      }
    }
    for (const id of dragIds) {
      const element = renderedElement(id);
      if (!element) continue;
      const state = useEditorStore.getState();
      const node = state.project ? findNode(state.project, id) : null;
      const rotation = node?.rotation ? ` rotate(${node.rotation}deg)` : "";
      element.classList.add("dragging");
      element.style.transform = `translate(${dragOffset.dx}px, ${dragOffset.dy}px)${rotation}`;
    }
    previousDragIds.current = dragIds;
  }, [dragIds, dragOffset, visibleRoot]);

  // Track the space key for drag-to-pan.
  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.code === "Space") {
        setSpaceDown(true);
      }
    };
    const up = (e: KeyboardEvent) => {
      if (e.code === "Space") {
        setSpaceDown(false);
      }
    };
    window.addEventListener("keydown", down);
    window.addEventListener("keyup", up);
    return () => {
      window.removeEventListener("keydown", down);
      window.removeEventListener("keyup", up);
    };
  }, []);

  // Keyboard editing: delete, duplicate, nudge, deselect. Ignored while typing.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const active = document.activeElement as HTMLElement | null;
      if (
        active &&
        (active.tagName === "INPUT" ||
          active.tagName === "TEXTAREA" ||
          active.tagName === "BUTTON" ||
          active.tagName === "SELECT" ||
          active.isContentEditable)
      ) {
        return;
      }

      const store = useEditorStore.getState();

      if ((e.ctrlKey || e.metaKey) && (e.key === "c" || e.key === "C")) {
        if (store.selectedIds.length > 0) {
          e.preventDefault();
          store.copySelection();
        }
        return;
      }

      if ((e.ctrlKey || e.metaKey) && (e.key === "x" || e.key === "X")) {
        if (store.selectedIds.length > 0) {
          e.preventDefault();
          store.cutSelection();
        }
        return;
      }

      if ((e.ctrlKey || e.metaKey) && (e.key === "v" || e.key === "V")) {
        if (store.canPaste) {
          e.preventDefault();
          store.pasteClipboard();
        }
        return;
      }

      if ((e.ctrlKey || e.metaKey) && (e.key === "z" || e.key === "Z")) {
        e.preventDefault();
        if (e.shiftKey) {
          store.redo();
        } else {
          store.undo();
        }
        return;
      }

      if ((e.ctrlKey || e.metaKey) && (e.key === "y" || e.key === "Y")) {
        e.preventDefault();
        store.redo();
        return;
      }

      if (e.key === "Escape") {
        store.selectElement(null);
        return;
      }

      if (store.selectedIds.length === 0) {
        return;
      }

      if (e.key === "Delete" || e.key === "Backspace") {
        e.preventDefault();
        store.deleteSelection();
        return;
      }

      if ((e.ctrlKey || e.metaKey) && (e.key === "d" || e.key === "D")) {
        e.preventDefault();
        store.duplicateSelection();
        return;
      }

      if ((e.ctrlKey || e.metaKey) && (e.key === "g" || e.key === "G")) {
        e.preventDefault();
        if (e.shiftKey) {
          store.ungroupElement(store.selectedIds[0]);
        } else {
          store.groupSelection();
        }
        return;
      }

      const step = e.shiftKey ? 10 : 1;
      switch (e.key) {
        case "ArrowLeft":
          e.preventDefault();
          store.moveSelectionBy(-step, 0);
          break;
        case "ArrowRight":
          e.preventDefault();
          store.moveSelectionBy(step, 0);
          break;
        case "ArrowUp":
          e.preventDefault();
          store.moveSelectionBy(0, -step);
          break;
        case "ArrowDown":
          e.preventDefault();
          store.moveSelectionBy(0, step);
          break;
        default:
          break;
      }
    };

    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  useEffect(() => {
    if (!contextMenu) return;
    const close = (event: KeyboardEvent | PointerEvent) => {
      if (event instanceof KeyboardEvent && event.key !== "Escape") return;
      setContextMenu(null);
    };
    window.addEventListener("pointerdown", close);
    window.addEventListener("keydown", close);
    return () => {
      window.removeEventListener("pointerdown", close);
      window.removeEventListener("keydown", close);
    };
  }, [contextMenu]);

  // Native wheel listener so we can preventDefault (React's is passive).
  useEffect(() => {
    const clip = clipRef.current;
    if (!clip) {
      return;
    }

    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const rect = clip.getBoundingClientRect();
      const cx = e.clientX - rect.left;
      const cy = e.clientY - rect.top;

      const state = useEditorStore.getState();
      if (e.ctrlKey || e.metaKey) {
        const z = state.zoom / 100;
        const nz = clamp(state.zoom * Math.exp(-e.deltaY * 0.0015)) / 100;
        const worldX = (cx - state.panX) / z;
        const worldY = (cy - state.panY) / z;
        setView({
          zoom: nz * 100,
          panX: cx - worldX * nz,
          panY: cy - worldY * nz,
        });
      } else {
        setView({ panX: state.panX - e.deltaX, panY: state.panY - e.deltaY });
      }
    };

    clip.addEventListener("wheel", onWheel, { passive: false });
    return () => clip.removeEventListener("wheel", onWheel);
  }, [setView]);

  const onPointerDown = useCallback(
    (e: React.PointerEvent) => {
      setContextMenu(null);
      // Panning takes priority (space-drag or middle mouse).
      if (e.button === 1 || (e.button === 0 && spaceDown)) {
        e.preventDefault();
        (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
        const state = useEditorStore.getState();
        panState.current = {
          startX: e.clientX,
          startY: e.clientY,
          panX: state.panX,
          panY: state.panY,
        };
        setIsPanning(true);
        return;
      }

      if (e.button !== 0) {
        return;
      }

      const target = (e.target as HTMLElement).closest(
        "[data-element-id]",
      ) as HTMLElement | null;
      const id = target?.dataset.elementId ?? null;
      const isRoot = target?.dataset.root === "true";
      const store = useEditorStore.getState();
      const shift = e.shiftKey;

      // Empty canvas / page root → begin a marquee selection.
      if (!id || isRoot || !store.project) {
        const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
        (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
        marquee.current = {
          startX: e.clientX - rect.left,
          startY: e.clientY - rect.top,
          additive: shift,
        };
        if (!shift) {
          store.selectElement(null);
        }
        return;
      }

      // Shift-click toggles an element in/out of the selection (no drag).
      if (shift) {
        store.toggleSelect(id);
        return;
      }

      const project = store.project;
      const alreadySelected = store.selectedIds.includes(id);
      const group = alreadySelected && store.selectedIds.length > 1;
      if (!group) {
        store.selectElement(id);
      }

      const parent = findParent(project, id);
      const abs = getAbsolutePosition(project, id);
      const nodeObj = parent?.children.find((c) => c.id === id) ?? null;
      if (!parent || !abs || !nodeObj) {
        return;
      }

      // Locked elements can be selected but not dragged/re-parented from the canvas.
      if (nodeObj.locked) {
        return;
      }

      (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
      elementDrag.current = {
        id,
        startClientX: e.clientX,
        startClientY: e.clientY,
        origParentId: parent.id,
        origLocalX: nodeObj.x,
        origLocalY: nodeObj.y,
        origAbsX: abs.x,
        origAbsY: abs.y,
        origW: nodeObj.width,
        origH: nodeObj.height,
        moved: false,
        ids: group ? [...store.selectedIds] : [id],
        group,
        exclude: subtreeIds(project, id),
      };
    },
    [spaceDown],
  );

  const onContextMenu = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    const target = (e.target as HTMLElement).closest(
      "[data-element-id]",
    ) as HTMLElement | null;
    const id =
      target?.dataset.root === "true" ? null : target?.dataset.elementId;
    const state = useEditorStore.getState();
    if (id && !state.selectedIds.includes(id)) state.selectElement(id);
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    setContextMenu({
      left: Math.min(e.clientX - rect.left, Math.max(0, rect.width - 180)),
      top: Math.min(e.clientY - rect.top, Math.max(0, rect.height - 250)),
    });
  }, []);

  const runContextAction = useCallback((action: () => void) => {
    action();
    setContextMenu(null);
  }, []);

  const onPointerMove = useCallback(
    (e: React.PointerEvent) => {
      // Panning.
      const pan = panState.current;
      if (pan) {
        setView({
          panX: pan.panX + (e.clientX - pan.startX),
          panY: pan.panY + (e.clientY - pan.startY),
        });
        return;
      }

      // Marquee rubber-band.
      const mq = marquee.current;
      if (mq && clipRef.current) {
        const rect = clipRef.current.getBoundingClientRect();
        const cx = e.clientX - rect.left;
        const cy = e.clientY - rect.top;
        setMarqueeRect({
          left: Math.min(mq.startX, cx),
          top: Math.min(mq.startY, cy),
          width: Math.abs(cx - mq.startX),
          height: Math.abs(cy - mq.startY),
        });
        return;
      }

      // Element dragging.
      const drag = elementDrag.current;
      if (!drag) {
        return;
      }

      const z = useEditorStore.getState().zoom / 100;
      const dx = (e.clientX - drag.startClientX) / z;
      const dy = (e.clientY - drag.startClientY) / z;

      if (
        !drag.moved &&
        Math.hypot(
          e.clientX - drag.startClientX,
          e.clientY - drag.startClientY,
        ) < DRAG_THRESHOLD
      ) {
        return;
      }
      drag.moved = true;

      setDragIds(drag.ids);

      // Single-element drags can re-parent into a container; group drags do not.
      if (drag.group) {
        setDragOffset({ dx, dy });
        setGuides({ x: null, y: null });
        return;
      }

      // Smart-guide snapping for single-element drags.
      const store = useEditorStore.getState();
      if (store.project) {
        const bp =
          store.project.breakpoints.find((b) => b.id === store.breakpointId) ??
          store.project.breakpoints.find((b) => b.isBase);
        const frameW = frameWidthFor(bp);
        const snap = computeSnap(
          drag,
          dx,
          dy,
          store.project,
          store.activePageId,
          frameW,
          FRAME_MIN_HEIGHT,
          store.zoom,
        );
        setDragOffset({ dx: snap.dx, dy: snap.dy });
        setGuides({
          x:
            snap.guideX !== null
              ? store.panX + snap.guideX * (store.zoom / 100)
              : null,
          y:
            snap.guideY !== null
              ? store.panY + snap.guideY * (store.zoom / 100)
              : null,
        });
      } else {
        setDragOffset({ dx, dy });
      }

      const draggedEl = document.querySelector(
        `[data-element-id="${drag.id}"]`,
      );
      const stack = document.elementsFromPoint(
        e.clientX,
        e.clientY,
      ) as HTMLElement[];
      let target: string | null = null;
      for (const el of stack) {
        const candidate = el.closest("[data-element-id]") as HTMLElement | null;
        if (!candidate) {
          continue;
        }
        if (
          draggedEl &&
          (candidate === draggedEl || draggedEl.contains(candidate))
        ) {
          continue;
        }
        const type = candidate.dataset.elementType ?? "";
        if (candidate.dataset.root === "true" || CONTAINER_TYPES.has(type)) {
          target = candidate.dataset.elementId ?? null;
          break;
        }
      }
      setDropTargetId(target);
    },
    [setView],
  );

  const endInteraction = useCallback(
    (e: React.PointerEvent) => {
      if (panState.current) {
        (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);
        panState.current = null;
        setIsPanning(false);
        return;
      }

      // Commit a marquee selection.
      const mq = marquee.current;
      if (mq) {
        marquee.current = null;
        (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);
        const rectScreen = marqueeRect;
        setMarqueeRect(null);

        const store = useEditorStore.getState();
        if (
          rectScreen &&
          (rectScreen.width > 2 || rectScreen.height > 2) &&
          store.project
        ) {
          const z = store.zoom / 100;
          const wx0 = (rectScreen.left - store.panX) / z;
          const wy0 = (rectScreen.top - store.panY) / z;
          const wx1 = (rectScreen.left + rectScreen.width - store.panX) / z;
          const wy1 = (rectScreen.top + rectScreen.height - store.panY) / z;
          const index = getProjectSpatialIndex(
            store.project,
            store.activePageId,
          );
          const hits = index
            ? queryElementRects(index, {
                x: wx0,
                y: wy0,
                width: wx1 - wx0,
                height: wy1 - wy0,
              }).map((r) => r.id)
            : [];
          store.selectMany(hits, mq.additive);
        }
        return;
      }

      const drag = elementDrag.current;
      elementDrag.current = null;
      if (!drag) {
        return;
      }
      (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);

      if (drag.moved) {
        const store = useEditorStore.getState();
        const project = store.project;
        if (project) {
          const { dx, dy } = dragOffset;
          if (drag.group) {
            store.moveSelectionBy(dx, dy);
          } else {
            const targetId = dropTargetId ?? drag.origParentId;
            if (targetId !== drag.origParentId) {
              const parentAbs = getAbsolutePosition(project, targetId) ?? {
                x: 0,
                y: 0,
              };
              store.reparentElement(
                drag.id,
                targetId,
                drag.origAbsX + dx - parentAbs.x,
                drag.origAbsY + dy - parentAbs.y,
              );
            } else {
              store.moveElement(
                drag.id,
                drag.origLocalX + dx,
                drag.origLocalY + dy,
              );
            }
          }
        }
      }

      setDragIds([]);
      setDragOffset({ dx: 0, dy: 0 });
      setDropTargetId(null);
      setGuides({ x: null, y: null });
    },
    [dragOffset, dropTargetId, marqueeRect],
  );

  const onDragOver = useCallback((e: React.DragEvent) => {
    if (
      e.dataTransfer.types.includes(DRAG_MIME) ||
      e.dataTransfer.types.includes(ASSET_DRAG_MIME) ||
      e.dataTransfer.types.includes(COMPONENT_DRAG_MIME) ||
      e.dataTransfer.types.includes("text/plain") ||
      e.dataTransfer.types.includes("Files")
    ) {
      e.preventDefault();
      e.dataTransfer.dropEffect = "copy";
    }
  }, []);

  const onDrop = useCallback(
    (e: React.DragEvent) => {
      const type = e.dataTransfer.getData(DRAG_MIME);
      const assetId = e.dataTransfer.getData(ASSET_DRAG_MIME);
      const componentPayload = parseComponentDragPayload(
        e.dataTransfer.getData(COMPONENT_DRAG_MIME) ||
          e.dataTransfer.getData("text/plain"),
      );
      const state = useEditorStore.getState();
      const droppedName = e.dataTransfer.files.item(0)?.name;
      const asset = state.project?.assets.find(
        (candidate) =>
          (assetId.length > 0 && candidate.id === assetId) ||
          (droppedName !== undefined &&
            candidate.storedFileName === droppedName),
      );
      if (!type && !asset && !componentPayload) {
        return;
      }
      e.preventDefault();

      const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
      const z = state.zoom / 100;
      const worldX = (e.clientX - rect.left - state.panX) / z;
      const worldY = (e.clientY - rect.top - state.panY) / z;
      if (componentPayload) {
        insertComponent(
          componentPayload.root,
          Math.max(0, worldX),
          Math.max(0, worldY),
        );
      } else if (asset) {
        insertAsset(asset, Math.max(0, worldX), Math.max(0, worldY));
      } else {
        insertElement(type, Math.max(0, worldX), Math.max(0, worldY));
      }
    },
    [insertAsset, insertComponent, insertElement],
  );

  const z = zoom / 100;
  const minor = 50 * z;
  const frameWidth = frameWidthFor(breakpoint);
  const panning = spaceDown || isPanning;

  return (
    <div className="canvas-root" style={{ background: canvasBackground }}>
      {project ? <style>{editorFontFaceCss(project)}</style> : null}
      <div
        className="canvas-corner"
        style={{ width: RULER_SIZE, height: RULER_SIZE }}
      />
      <div
        className="canvas-ruler-h"
        style={{ left: RULER_SIZE, height: RULER_SIZE }}
      >
        <Ruler
          orientation="horizontal"
          length={Math.max(0, width)}
          zoom={zoom}
          pan={panX}
        />
      </div>
      <div
        className="canvas-ruler-v"
        style={{ top: RULER_SIZE, width: RULER_SIZE }}
      >
        <Ruler
          orientation="vertical"
          length={Math.max(0, height)}
          zoom={zoom}
          pan={panY}
        />
      </div>

      <div
        ref={clipRef}
        className={`canvas-clip${panning ? " panning" : ""}`}
        tabIndex={0}
        role="region"
        aria-label="Design canvas. Use arrow keys to move the selection, Delete to remove, and Control D to duplicate."
        style={{ left: RULER_SIZE, top: RULER_SIZE }}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endInteraction}
        onPointerCancel={endInteraction}
        onContextMenu={onContextMenu}
        onDragOver={onDragOver}
        onDrop={onDrop}
      >
        <div
          className="canvas-grid"
          style={{
            backgroundSize: `${minor}px ${minor}px`,
            backgroundPosition: `${panX}px ${panY}px`,
          }}
        />

        {visibleRoot ? (
          <div
            className="canvas-viewport"
            style={{ transform: `translate(${panX}px, ${panY}px) scale(${z})` }}
          >
            <div
              className="device-frame"
              style={{ width: frameWidth, minHeight: FRAME_MIN_HEIGHT }}
            >
              <CanvasRenderContext.Provider value={renderContext}>
                <ElementRenderer
                  node={visibleRoot}
                  isRoot
                  frameMinHeight={FRAME_MIN_HEIGHT}
                />
              </CanvasRenderContext.Provider>
            </div>
          </div>
        ) : (
          <div className="canvas-empty">Loading project…</div>
        )}

        <SelectionOverlay />

        {contextMenu ? (
          <div
            className="canvas-context-menu"
            role="menu"
            aria-label="Canvas actions"
            style={{ left: contextMenu.left, top: contextMenu.top }}
            onPointerDown={(event) => event.stopPropagation()}
          >
            <button
              type="button"
              role="menuitem"
              disabled={selectedIds.length === 0}
              onClick={() =>
                runContextAction(() => useEditorStore.getState().cutSelection())
              }
            >
              Cut <kbd>Ctrl+X</kbd>
            </button>
            <button
              type="button"
              role="menuitem"
              disabled={selectedIds.length === 0}
              onClick={() =>
                runContextAction(() =>
                  useEditorStore.getState().copySelection(),
                )
              }
            >
              Copy <kbd>Ctrl+C</kbd>
            </button>
            <button
              type="button"
              role="menuitem"
              disabled={!useEditorStore.getState().canPaste}
              onClick={() =>
                runContextAction(() =>
                  useEditorStore.getState().pasteClipboard(),
                )
              }
            >
              Paste <kbd>Ctrl+V</kbd>
            </button>
            <div className="context-menu-separator" role="separator" />
            <button
              type="button"
              role="menuitem"
              disabled={selectedIds.length === 0}
              onClick={() =>
                runContextAction(() =>
                  useEditorStore.getState().duplicateSelection(),
                )
              }
            >
              Duplicate <kbd>Ctrl+D</kbd>
            </button>
            <button
              type="button"
              role="menuitem"
              disabled={selectedIds.length === 0}
              onClick={() =>
                runContextAction(() =>
                  useEditorStore.getState().deleteSelection(),
                )
              }
            >
              Delete <kbd>Del</kbd>
            </button>
          </div>
        ) : null}

        {marqueeRect ? (
          <div
            className="marquee"
            style={{
              left: marqueeRect.left,
              top: marqueeRect.top,
              width: marqueeRect.width,
              height: marqueeRect.height,
            }}
          />
        ) : null}

        {guides.x !== null ? (
          <div className="snap-guide snap-guide-v" style={{ left: guides.x }} />
        ) : null}
        {guides.y !== null ? (
          <div className="snap-guide snap-guide-h" style={{ top: guides.y }} />
        ) : null}
      </div>
    </div>
  );
}
