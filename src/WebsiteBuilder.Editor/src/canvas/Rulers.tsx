interface RulerProps {
  orientation: "horizontal" | "vertical";
  /** Length of the ruler in screen pixels. */
  length: number;
  /** Zoom percentage (100 = 1:1). */
  zoom: number;
  /** Pan offset along this axis, in screen pixels. */
  pan: number;
}

const CANDIDATE_STEPS = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 5000];
const TARGET_LABEL_SPACING = 80; // px between numeric labels

/** Chooses a "nice" world-unit step so labels land ~80px apart at the given zoom. */
function chooseStep(zoom: number): number {
  const z = zoom / 100;
  const worldTarget = TARGET_LABEL_SPACING / z;
  return CANDIDATE_STEPS.find((s) => s >= worldTarget) ?? 10000;
}

interface Tick {
  screen: number;
  label: number;
}

function buildTicks(length: number, zoom: number, pan: number): Tick[] {
  const z = zoom / 100;
  const step = chooseStep(zoom);

  const worldStart = (0 - pan) / z;
  const worldEnd = (length - pan) / z;
  const first = Math.ceil(worldStart / step) * step;

  const ticks: Tick[] = [];
  for (let world = first; world <= worldEnd; world += step) {
    ticks.push({ screen: pan + world * z, label: Math.round(world) });
  }
  return ticks;
}

/**
 * A numbered ruler aligned with the canvas viewport. Ticks are computed from the
 * current zoom and pan so the measurements always reflect what is on screen.
 */
export default function Ruler({ orientation, length, zoom, pan }: RulerProps) {
  const horizontal = orientation === "horizontal";
  const ticks = buildTicks(length, zoom, pan);

  return (
    <div className={`ruler ruler-${orientation}`}>
      {ticks.map((tick) => (
        <div
          key={tick.label}
          className="ruler-tick"
          style={horizontal ? { left: `${tick.screen}px` } : { top: `${tick.screen}px` }}
        >
          <span className="ruler-label">{tick.label}</span>
        </div>
      ))}
    </div>
  );
}
