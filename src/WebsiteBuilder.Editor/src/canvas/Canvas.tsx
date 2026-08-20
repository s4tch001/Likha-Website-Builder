import { useCallback, useEffect, useRef, useState } from "react";
import { frameWidthFor } from "../model/types";
import { DRAG_MIME } from "../palette/catalog";
import { ASSET_DRAG_MIME, editorFontFaceCss } from "../model/assetElements";
import {
  COMPONENT_DRAG_MIME,
  parseComponentDragPayload,
} from "../model/componentDrag";
import {
  collectElementRects,
  getAbsolutePosition,
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

const RULER_SIZE = 24;
const FRAME_MIN_HEIGHT = 1000;
const DRAG_THRESHOLD = 3; // px before a press becomes a move

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
          active.isContentEditable)
      ) {
        return;
      }

      const store = useEditorStore.getState();

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
          const hits = collectElementRects(store.project, store.activePageId)
            .filter(
              (r) =>
                r.x < wx1 && r.x + r.w > wx0 && r.y < wy1 && r.y + r.h > wy0,
            )
            .map((r) => r.id);
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
        style={{ left: RULER_SIZE, top: RULER_SIZE }}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endInteraction}
        onPointerCancel={endInteraction}
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

        {page ? (
          <div
            className="canvas-viewport"
            style={{ transform: `translate(${panX}px, ${panY}px) scale(${z})` }}
          >
            <div
              className="device-frame"
              style={{ width: frameWidth, minHeight: FRAME_MIN_HEIGHT }}
            >
              <CanvasRenderContext.Provider
                value={{
                  selectedIds,
                  dropTargetId,
                  dragIds,
                  dragDX: dragOffset.dx,
                  dragDY: dragOffset.dy,
                  breakpoints: breakpoints ?? [],
                  breakpointId,
                }}
              >
                <ElementRenderer
                  node={page.root}
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
