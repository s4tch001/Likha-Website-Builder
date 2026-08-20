import { createContext } from "react";
import type { BreakpointDef } from "../model/types";

const EMPTY_ASSET_URLS: ReadonlyMap<string, string> = new Map();

/** Stable model-derived render inputs shared with every rendered element. */
export interface CanvasRenderState {
  /** Breakpoints and the active one, so the renderer can apply the responsive cascade. */
  breakpoints: readonly BreakpointDef[];
  breakpointId: string | null;
  /** Canonical managed asset path to editor virtual-origin URL. */
  assetUrls: ReadonlyMap<string, string>;
}

export const CanvasRenderContext = createContext<CanvasRenderState>({
  breakpoints: [],
  breakpointId: null,
  assetUrls: EMPTY_ASSET_URLS,
});
