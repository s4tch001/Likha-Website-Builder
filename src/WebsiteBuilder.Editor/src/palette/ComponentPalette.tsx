import { DRAG_MIME, PALETTE, type PaletteItem } from "./catalog";
import { useEditorStore } from "../store/editorStore";

/**
 * The in-canvas component palette. Each entry is an HTML5 drag source; dropping
 * it on the canvas creates a new element of that type (handled in Canvas.tsx).
 */
export default function ComponentPalette() {
  const insertElement = useEditorStore((state) => state.insertElement);

  const onDragStart = (e: React.DragEvent, item: PaletteItem) => {
    e.dataTransfer.setData(DRAG_MIME, item.type);
    e.dataTransfer.effectAllowed = "copy";
  };

  const insertFromKeyboardOrClick = (item: PaletteItem) => {
    const revision = useEditorStore.getState().revision;
    const stagger = revision % 6;
    insertElement(item.type, 80 + stagger * 24, 80 + stagger * 24);
  };

  return (
    <aside className="palette" aria-label="Element palette">
      <h2 className="palette-title">ELEMENTS</h2>
      <div className="palette-scroll">
        {PALETTE.map((group) => (
          <div key={group.name} className="palette-group">
            <div className="palette-group-name">{group.name}</div>
            <div className="palette-grid">
              {group.items.map((item) => (
                <button
                  key={item.type}
                  type="button"
                  className="palette-item"
                  draggable
                  onDragStart={(e) => onDragStart(e, item)}
                  onClick={() => insertFromKeyboardOrClick(item)}
                  title={`Add ${item.label}; drag to choose a position`}
                >
                  <span className="palette-glyph" aria-hidden="true">
                    {item.glyph}
                  </span>
                  <span className="palette-label">{item.label}</span>
                </button>
              ))}
            </div>
          </div>
        ))}
      </div>
    </aside>
  );
}
