import type { ElementNode } from "./types";

/** A short unique id for a newly created element. */
function newId(type: string): string {
  const suffix =
    typeof crypto.randomUUID === "function"
      ? crypto.randomUUID().slice(0, 8)
      : Math.random().toString(36).slice(2, 10);
  return `${type.toLowerCase()}-${suffix}`;
}

interface ElementDefaults {
  width: number;
  height: number;
  text?: string;
  styles?: Record<string, string>;
}

/**
 * Per-type starting geometry, text and styling for a freshly dropped element.
 * Mirrors the catalog the host exposes; values are sensible, editable defaults.
 */
const DEFAULTS: Record<string, ElementDefaults> = {
  Section: { width: 600, height: 240, styles: { background: "#1a1a20", "border-radius": "8px" } },
  Container: { width: 480, height: 200, styles: { background: "#1f1f26", "border-radius": "8px" } },
  Div: { width: 240, height: 140, styles: { background: "#23232b", "border-radius": "6px" } },
  Navbar: { width: 720, height: 64, styles: { background: "#16161b", "border-radius": "8px" } },
  Sidebar: { width: 240, height: 360, styles: { background: "#16161b", "border-radius": "8px" } },
  Footer: { width: 720, height: 120, styles: { background: "#16161b", "border-radius": "8px" } },
  Card: { width: 320, height: 200, styles: { background: "#1a1a20", border: "1px solid #2c2c34", "border-radius": "12px" } },
  Heading: {
    width: 360,
    height: 48,
    text: "Heading",
    styles: { color: "#f5f5fa", "font-size": "32px", "font-weight": "700" },
  },
  Paragraph: {
    width: 360,
    height: 72,
    text: "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
    styles: { color: "#c4c4cc", "font-size": "16px", "line-height": "1.5" },
  },
  Text: { width: 160, height: 28, text: "Text", styles: { color: "#e6e6eb", "font-size": "15px" } },
  Link: { width: 120, height: 24, text: "Link", styles: { color: "#3b82f6", "font-size": "15px", "text-decoration": "underline" } },
  Button: {
    width: 160,
    height: 44,
    text: "Button",
    styles: {
      background: "#2563eb",
      color: "#ffffff",
      "font-size": "15px",
      "font-weight": "600",
      "border-radius": "8px",
      display: "flex",
      "align-items": "center",
      "justify-content": "center",
    },
  },
  Image: { width: 240, height: 160, styles: { background: "#2c2c34", "border-radius": "6px" } },
  Video: { width: 320, height: 180, styles: { background: "#111", "border-radius": "6px" } },
  Icon: { width: 40, height: 40, text: "★", styles: { color: "#e6e6eb", "font-size": "28px", display: "flex", "align-items": "center", "justify-content": "center" } },
  Input: { width: 240, height: 40, styles: { background: "#fff", border: "1px solid #cbd5e1", "border-radius": "6px" } },
  Textarea: { width: 280, height: 100, styles: { background: "#fff", border: "1px solid #cbd5e1", "border-radius": "6px" } },
  Badge: { width: 64, height: 24, text: "Badge", styles: { background: "#2563eb", color: "#fff", "border-radius": "999px", "font-size": "12px", display: "flex", "align-items": "center", "justify-content": "center" } },
  Alert: { width: 360, height: 56, text: "Alert message", styles: { background: "#3a2317", color: "#fbbf24", border: "1px solid #b45309", "border-radius": "8px", display: "flex", "align-items": "center", "padding-left": "12px" } },
};

const FALLBACK: ElementDefaults = { width: 160, height: 80, styles: { background: "#23232b", "border-radius": "6px" } };

/**
 * Creates a new element of the given type positioned at (x, y) within its parent.
 * Coordinates are snapped to whole pixels.
 */
export function createElement(type: string, x: number, y: number): ElementNode {
  const defaults = DEFAULTS[type] ?? FALLBACK;
  return {
    id: newId(type),
    type,
    name: type,
    x: Math.round(x),
    y: Math.round(y),
    width: defaults.width,
    height: defaults.height,
    rotation: 0,
    text: defaults.text,
    attributes: {},
    styles: { ...(defaults.styles ?? {}) },
    responsiveStyles: {},
    hidden: false,
    locked: false,
    children: [],
  };
}
