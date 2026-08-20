import { DRAG_MIME, PALETTE, type PaletteItem } from "./catalog";

/**
 * The in-canvas component palette. Each entry is an HTML5 drag source; dropping
 * it on the canvas creates a new element of that type (handled in Canvas.tsx).
 */
export default function ComponentPalette() {
  const onDragStart = (e: React.DragEvent, item: PaletteItem) => {
    e.dataTransfer.setData(DRAG_MIME, item.type);
    e.dataTransfer.effectAllowed = "copy";
  };

  return (
    <div className="palette">
      <div className="palette-title">ELEMENTS</div>
      <div className="palette-scroll">
        {PALETTE.map((group) => (
          <div key={group.name} className="palette-group">
            <div className="palette-group-name">{group.name}</div>
            <div className="palette-grid">
              {group.items.map((item) => (
                <div
                  key={item.type}
                  className="palette-item"
                  draggable
                  onDragStart={(e) => onDragStart(e, item)}
                  title={`Drag to add a ${item.label}`}
                >
                  <span className="palette-glyph">{item.glyph}</span>
                  <span className="palette-label">{item.label}</span>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
