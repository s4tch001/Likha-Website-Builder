import type { CSSProperties } from "react";
import type { ElementNode } from "../model/types";

/** Converts a kebab-case CSS property name to the camelCase React expects. */
function toCamel(prop: string): string {
  // Preserve custom properties (CSS variables) untouched.
  if (prop.startsWith("--")) {
    return prop;
  }
  return prop.replace(/-([a-z])/g, (_, c: string) => c.toUpperCase());
}

/** Builds a React style object from an element's CSS declaration map. */
export function toReactStyle(styles: Record<string, string>): CSSProperties {
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(styles)) {
    result[toCamel(key)] = value;
  }
  return result as CSSProperties;
}

/**
 * Computes the absolute layout box for a non-root node. Elements store explicit
 * geometry (x/y/width/height) which positions them within their parent; the
 * style map then layers on appearance.
 */
export function geometryStyle(node: ElementNode): CSSProperties {
  return {
    position: "absolute",
    left: `${node.x}px`,
    top: `${node.y}px`,
    width: node.width > 0 ? `${node.width}px` : undefined,
    height: node.height > 0 ? `${node.height}px` : undefined,
  };
}
