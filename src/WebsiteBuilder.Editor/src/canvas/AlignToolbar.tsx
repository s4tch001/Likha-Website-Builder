import type { AlignMode } from "../store/editorStore";
import { useEditorStore } from "../store/editorStore";

interface AlignButton {
  mode: AlignMode;
  glyph: string;
  title: string;
  needsThree?: boolean;
}

const BUTTONS: AlignButton[] = [
  { mode: "left", glyph: "⊏", title: "Align left" },
  { mode: "hcenter", glyph: "⊞", title: "Align horizontal centers" },
  { mode: "right", glyph: "⊐", title: "Align right" },
  { mode: "top", glyph: "⊤", title: "Align top" },
  { mode: "vmiddle", glyph: "⊟", title: "Align vertical centers" },
  { mode: "bottom", glyph: "⊥", title: "Align bottom" },
  {
    mode: "distH",
    glyph: "↔",
    title: "Distribute horizontally",
    needsThree: true,
  },
  {
    mode: "distV",
    glyph: "↕",
    title: "Distribute vertically",
    needsThree: true,
  },
];

/**
 * Floating alignment toolbar shown when two or more elements are selected. Each
 * button aligns/distributes the selection via the store (the same `alignSelection`
 * action the WPF Arrange ribbon drives over the bridge).
 */
export default function AlignToolbar() {
  const count = useEditorStore((s) => s.selectedIds.length);
  const align = useEditorStore((s) => s.alignSelection);

  if (count < 2) {
    return null;
  }

  return (
    <div className="align-toolbar">
      {BUTTONS.map((b, i) => (
        <button
          key={b.mode}
          className={`align-btn${i === 6 ? " align-sep" : ""}`}
          title={b.title}
          disabled={b.needsThree && count < 3}
          onClick={() => align(b.mode)}
        >
          {b.glyph}
        </button>
      ))}
    </div>
  );
}
