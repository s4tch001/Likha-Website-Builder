import { describe, expect, it } from "vitest";
import type { ElementNode } from "./types";
import {
  COMPONENT_TEXT_PREFIX,
  parseComponentDragPayload,
} from "./componentDrag";

function root(): ElementNode {
  return {
    id: "template-root",
    type: "Section",
    x: 0,
    y: 0,
    width: 400,
    height: 200,
    rotation: 0,
    attributes: {},
    styles: {},
    responsiveStyles: {},
    hidden: false,
    locked: false,
    children: [],
  };
}

describe("component drag payload", () => {
  it("accepts a prefixed validated envelope", () => {
    const text =
      COMPONENT_TEXT_PREFIX +
      JSON.stringify({ componentId: "hero-simple", root: root() });
    expect(parseComponentDragPayload(text)?.componentId).toBe("hero-simple");
  });

  it("rejects malformed, oversized, and unsafe envelopes", () => {
    expect(parseComponentDragPayload("not json")).toBeNull();
    expect(parseComponentDragPayload("x".repeat(1_000_001))).toBeNull();
    const unsafe = root();
    unsafe.attributes.onclick = "alert(1)";
    expect(
      parseComponentDragPayload(
        JSON.stringify({ componentId: "bad", root: unsafe }),
      ),
    ).toBeNull();
  });
});
