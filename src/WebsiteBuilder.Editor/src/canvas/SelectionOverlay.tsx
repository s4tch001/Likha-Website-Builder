import { useCallback, useRef } from "react";
import {
  findNode,
  getAbsolutePosition,
  useEditorStore,
} from "../store/editorStore";

interface Handle {
  dir: string;
  left?: boolean;
  right?: boolean;
  top?: boolean;
  bottom?: boolean;
  cursor: string;
  /** Fractional anchor position within the box (0..1). */
  x: number;
  y: number;
}

/** The eight resize handles, with the box edges each one drives. */
const HANDLES: Handle[] = [
  { dir: "nw", left: true, top: true, cursor: "nwse-resize", x: 0, y: 0 },
  { dir: "n", top: true, cursor: "ns-resize", x: 0.5, y: 0 },
  { dir: "ne", right: true, top: true, cursor: "nesw-resize", x: 1, y: 0 },
  { dir: "e", right: true, cursor: "ew-resize", x: 1, y: 0.5 },
  { dir: "se", right: true, bottom: true, cursor: "nwse-resize", x: 1, y: 1 },
  { dir: "s", bottom: true, cursor: "ns-resize", x: 0.5, y: 1 },
  { dir: "sw", left: true, bottom: true, cursor: "nesw-resize", x: 0, y: 1 },
  { dir: "w", left: true, cursor: "ew-resize", x: 0, y: 0.5 },
];

interface ResizeStart {
  id: string;
  handle: Handle;
  startClientX: number;
  startClientY: number;
  x: number;
  y: number;
  w: number;
  h: number;
}

/**
 * Draws a selection box around every selected element (screen space, so the
 * outline and handles stay a constant size at any zoom). When exactly one element
 * is selected it also shows eight resize handles that resize it live.
 */
export default function SelectionOverlay() {
  const selectedIds = useEditorStore((s) => s.selectedIds);
  const project = useEditorStore((s) => s.project);
  const zoom = useEditorStore((s) => s.zoom);
  const panX = useEditorStore((s) => s.panX);
  const panY = useEditorStore((s) => s.panY);

  const resize = useRef<ResizeStart | null>(null);

  const onHandleDown = useCallback((e: React.PointerEvent, handle: Handle) => {
    e.stopPropagation();
    e.preventDefault();
    const state = useEditorStore.getState();
    const id = state.selectedIds[0];
    if (!id || !state.project) {
      return;
    }
    const node = findNode(state.project, id);
    if (!node) {
      return;
    }
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    resize.current = {
      id,
      handle,
      startClientX: e.clientX,
      startClientY: e.clientY,
      x: node.x,
      y: node.y,
      w: node.width,
      h: node.height,
    };
  }, []);

  const onHandleMove = useCallback((e: React.PointerEvent) => {
    const r = resize.current;
    if (!r) {
      return;
    }
    const z = useEditorStore.getState().zoom / 100;
    const dx = (e.clientX - r.startClientX) / z;
    const dy = (e.clientY - r.startClientY) / z;

    let { x, y, w, h } = r;
    if (r.handle.left) {
      x = r.x + dx;
      w = r.w - dx;
    }
    if (r.handle.right) {
      w = r.w + dx;
    }
    if (r.handle.top) {
      y = r.y + dy;
      h = r.h - dy;
    }
    if (r.handle.bottom) {
      h = r.h + dy;
    }

    useEditorStore.getState().resizeElement(r.id, x, y, w, h);
  }, []);

  const onHandleUp = useCallback((e: React.PointerEvent) => {
    if (resize.current) {
      (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);
      resize.current = null;
    }
  }, []);

  const rotateState = useRef<{ id: string; cx: number; cy: number } | null>(
    null,
  );

  const onRotateDown = useCallback((e: React.PointerEvent) => {
    e.stopPropagation();
    e.preventDefault();
    const id = useEditorStore.getState().selectedIds[0];
    if (!id) {
      return;
    }
    // The element's rotation centre is invariant under rotation, so capture it once.
    const el = document.querySelector(`[data-element-id="${id}"]`);
    if (!el) {
      return;
    }
    const rect = el.getBoundingClientRect();
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    rotateState.current = {
      id,
      cx: rect.left + rect.width / 2,
      cy: rect.top + rect.height / 2,
    };
  }, []);

  const onRotateMove = useCallback((e: React.PointerEvent) => {
    const rs = rotateState.current;
    if (!rs) {
      return;
    }
    let deg =
      (Math.atan2(e.clientY - rs.cy, e.clientX - rs.cx) * 180) / Math.PI + 90;
    if (e.shiftKey) {
      deg = Math.round(deg / 15) * 15;
    }
    useEditorStore.getState().rotateElement(rs.id, deg);
  }, []);

  const onRotateUp = useCallback((e: React.PointerEvent) => {
    if (rotateState.current) {
      (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);
      rotateState.current = null;
    }
  }, []);

  if (selectedIds.length === 0 || !project) {
    return null;
  }

  const z = zoom / 100;
  const single = selectedIds.length === 1;

  return (
    <>
      {selectedIds.map((id) => {
        const node = findNode(project, id);
        const abs = getAbsolutePosition(project, id);
        if (!node || !abs) {
          return null;
        }
        const left = panX + abs.x * z;
        const top = panY + abs.y * z;
        const w = node.width * z;
        const h = node.height * z;
        // Locked elements show the selection box but no resize/rotate handles.
        const editable = single && !node.locked;

        return (
          <div
            key={id}
            className={
              node.locked ? "selection-overlay locked" : "selection-overlay"
            }
            style={{
              left,
              top,
              width: w,
              height: h,
              transform: node.rotation
                ? `rotate(${node.rotation}deg)`
                : undefined,
              transformOrigin: "center",
            }}
          >
            <div className="selection-box" />
            {editable && (
              <div
                className="rotate-handle"
                title="Drag to rotate (Shift = 15° steps)"
                onPointerDown={onRotateDown}
                onPointerMove={onRotateMove}
                onPointerUp={onRotateUp}
                onPointerCancel={onRotateUp}
              />
            )}
            {editable &&
              HANDLES.map((handle) => (
                <div
                  key={handle.dir}
                  className="selection-handle"
                  style={{
                    left: `${handle.x * 100}%`,
                    top: `${handle.y * 100}%`,
                    cursor: handle.cursor,
                  }}
                  onPointerDown={(e) => onHandleDown(e, handle)}
                  onPointerMove={onHandleMove}
                  onPointerUp={onHandleUp}
                  onPointerCancel={onHandleUp}
                />
              ))}
          </div>
        );
      })}
    </>
  );
}
