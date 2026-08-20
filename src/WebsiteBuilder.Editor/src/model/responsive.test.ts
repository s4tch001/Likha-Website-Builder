import { describe, expect, it } from "vitest";
import type { BreakpointDef, ElementNode } from "./types";
import { effectiveStyles } from "./responsive";

const breakpoints: BreakpointDef[] = [
  { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
  { id: "laptop", label: "Laptop", maxWidth: 1280, isBase: false },
  { id: "tablet", label: "Tablet", maxWidth: 992, isBase: false },
  { id: "mobile", label: "Mobile", maxWidth: 480, isBase: false },
];

function node(): ElementNode {
  return {
    id: "n",
    type: "Div",
    x: 0,
    y: 0,
    width: 100,
    height: 100,
    rotation: 0,
    attributes: {},
    styles: { color: "black", "font-size": "20px" },
    responsiveStyles: {
      laptop: { "font-size": "18px" },
      tablet: { "font-size": "16px", color: "blue" },
      mobile: { "font-size": "12px" },
    },
    hidden: false,
    locked: false,
    children: [],
  };
}

describe("effectiveStyles cascade", () => {
  it("returns base styles at the base breakpoint", () => {
    const s = effectiveStyles(node(), breakpoints, "desktop");
    expect(s["font-size"]).toBe("20px");
    expect(s.color).toBe("black");
  });

  it("applies the active override at that breakpoint", () => {
    const s = effectiveStyles(node(), breakpoints, "laptop");
    expect(s["font-size"]).toBe("18px");
    expect(s.color).toBe("black"); // not overridden at laptop
  });

  it("cascades wider overrides down and lets the active one win", () => {
    // tablet inherits laptop's font-size unless it sets its own (it does → 16px),
    // and adds color:blue.
    const s = effectiveStyles(node(), breakpoints, "tablet");
    expect(s["font-size"]).toBe("16px");
    expect(s.color).toBe("blue");
  });

  it("inherits a wider override when the active breakpoint lacks its own", () => {
    const n = node();
    delete n.responsiveStyles.tablet["font-size"]; // tablet now only sets color
    const s = effectiveStyles(n, breakpoints, "tablet");
    expect(s["font-size"]).toBe("18px"); // inherited from laptop
    expect(s.color).toBe("blue");
  });

  it("does not apply overrides from narrower breakpoints than the active one", () => {
    const s = effectiveStyles(node(), breakpoints, "laptop");
    // mobile (12px) and tablet (16px) are narrower than laptop → ignored.
    expect(s["font-size"]).toBe("18px");
  });

  it("applies the full chain at the narrowest breakpoint", () => {
    const s = effectiveStyles(node(), breakpoints, "mobile");
    expect(s["font-size"]).toBe("12px"); // mobile wins
    expect(s.color).toBe("blue"); // inherited from tablet
  });
});
