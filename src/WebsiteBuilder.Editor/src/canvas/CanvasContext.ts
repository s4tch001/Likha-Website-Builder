import { createContext } from "react";
import type { BreakpointDef } from "../model/types";

/** Render-time canvas state shared with every rendered element. */
export interface CanvasRenderState {
  selectedIds: string[];
  /** Container currently highlighted as a drop target during a drag. */
  dropTargetId: string | null;
  /** Elements being dragged together, with their live offset in world pixels. */
  dragIds: string[];
  dragDX: number;
  dragDY: number;
  /** Breakpoints and the active one, so the renderer can apply the responsive cascade. */
  breakpoints: BreakpointDef[];
  breakpointId: string | null;
}

export const CanvasRenderContext = createContext<CanvasRenderState>({
  selectedIds: [],
  dropTargetId: null,
  dragIds: [],
  dragDX: 0,
  dragDY: 0,
  breakpoints: [],
  breakpointId: null,
});
