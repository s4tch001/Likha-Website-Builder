/** A draggable palette entry. */
export interface PaletteItem {
  type: string;
  label: string;
  glyph: string;
}

export interface PaletteGroup {
  name: string;
  items: PaletteItem[];
}

/**
 * The element catalog shown in the in-canvas palette. Mirrors the host's
 * ComponentCatalog; dragging an item onto the canvas creates that element.
 */
export const PALETTE: PaletteGroup[] = [
  {
    name: "Layout",
    items: [
      { type: "Section", label: "Section", glyph: "▭" },
      { type: "Container", label: "Container", glyph: "▢" },
      { type: "Div", label: "Div", glyph: "◻" },
      { type: "Navbar", label: "Navbar", glyph: "≡" },
      { type: "Sidebar", label: "Sidebar", glyph: "▥" },
      { type: "Footer", label: "Footer", glyph: "▁" },
      { type: "Card", label: "Card", glyph: "🂠" },
    ],
  },
  {
    name: "Typography",
    items: [
      { type: "Heading", label: "Heading", glyph: "H" },
      { type: "Paragraph", label: "Paragraph", glyph: "¶" },
      { type: "Text", label: "Text", glyph: "T" },
      { type: "Link", label: "Link", glyph: "🔗" },
    ],
  },
  {
    name: "Interactive",
    items: [
      { type: "Button", label: "Button", glyph: "⬚" },
      { type: "Input", label: "Input", glyph: "▭" },
      { type: "Textarea", label: "Textarea", glyph: "▦" },
    ],
  },
  {
    name: "Media",
    items: [
      { type: "Image", label: "Image", glyph: "🖼" },
      { type: "Video", label: "Video", glyph: "▶" },
      { type: "Icon", label: "Icon", glyph: "★" },
    ],
  },
  {
    name: "Feedback",
    items: [
      { type: "Badge", label: "Badge", glyph: "●" },
      { type: "Alert", label: "Alert", glyph: "⚠" },
    ],
  },
];

/** MIME type used to carry the element type through an HTML5 drag. */
export const DRAG_MIME = "application/x-wb-element-type";
