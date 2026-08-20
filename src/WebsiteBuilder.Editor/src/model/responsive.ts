import type { BreakpointDef, ElementNode } from "./types";

/**
 * Effective screen width of a breakpoint for cascade ordering. The base (or any
 * unbounded) breakpoint is treated as the widest possible.
 */
export function effectiveWidth(bp: BreakpointDef): number {
  return bp.isBase || bp.maxWidth <= 0 ? Number.POSITIVE_INFINITY : bp.maxWidth;
}

/**
 * Resolves an element's effective CSS for a given breakpoint using a desktop-first
 * cascade: start from the base styles, then apply per-breakpoint overrides from the
 * widest breakpoint down to (and including) the active one. Overrides defined only
 * for breakpoints narrower than the active one do not apply.
 */
export function effectiveStyles(
  node: ElementNode,
  breakpoints: BreakpointDef[],
  activeBreakpointId: string | null,
): Record<string, string> {
  const result: Record<string, string> = { ...node.styles };

  const active = breakpoints.find((b) => b.id === activeBreakpointId);
  if (!active || active.isBase) {
    return result; // base = the styles themselves, no overrides apply
  }

  const activeWidth = effectiveWidth(active);
  // Widest → narrowest so wider overrides are applied first and the active
  // breakpoint (the narrowest of those that apply) wins.
  const ordered = [...breakpoints].sort((a, b) => effectiveWidth(b) - effectiveWidth(a));
  for (const bp of ordered) {
    if (bp.isBase || effectiveWidth(bp) < activeWidth) {
      continue;
    }
    const override = node.responsiveStyles?.[bp.id];
    if (override) {
      Object.assign(result, override);
    }
  }

  return result;
}
