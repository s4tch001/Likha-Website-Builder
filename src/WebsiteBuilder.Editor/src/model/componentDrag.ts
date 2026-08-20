import type { ElementNode } from "./types";
import { isValidElementTree } from "./projectValidation";

export const COMPONENT_DRAG_MIME = "application/x-wb-component";
export const COMPONENT_TEXT_PREFIX = "likha-component:";
const MAX_COMPONENT_DRAG_CHARS = 1_000_000;
const componentId = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export interface ComponentDragEnvelope {
  componentId: string;
  root: ElementNode;
}

/** Parses a bounded drag payload and reuses the bridge-grade element-tree policy. */
export function parseComponentDragPayload(
  value: string,
): ComponentDragEnvelope | null {
  const json = value.startsWith(COMPONENT_TEXT_PREFIX)
    ? value.slice(COMPONENT_TEXT_PREFIX.length)
    : value;
  if (json.length === 0 || json.length > MAX_COMPONENT_DRAG_CHARS) {
    return null;
  }

  try {
    const parsed = JSON.parse(json) as {
      componentId?: unknown;
      root?: unknown;
    };
    if (
      typeof parsed.componentId !== "string" ||
      !componentId.test(parsed.componentId) ||
      !isValidElementTree(parsed.root)
    ) {
      return null;
    }
    return { componentId: parsed.componentId, root: parsed.root };
  } catch {
    return null;
  }
}
