import { describe, expect, it } from "vitest";
import type { Project } from "./types";
import {
  isSafeCssPropertyName,
  isSafeCssValue,
  isValidProject,
} from "./projectValidation";

function project(): Project {
  return {
    schemaVersion: 2,
    id: "project-1",
    name: "Safe",
    createdUtc: "2026-08-20T00:00:00Z",
    modifiedUtc: "2026-08-20T00:00:00Z",
    breakpoints: [
      { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
    ],
    variables: {},
    assets: [],
    pages: [
      {
        id: "page-1",
        name: "Home",
        route: "index",
        root: {
          id: "root-1",
          type: "Section",
          x: 0,
          y: 0,
          width: 0,
          height: 0,
          rotation: 0,
          attributes: {},
          styles: {},
          responsiveStyles: {},
          hidden: false,
          locked: false,
          children: [],
        },
      },
    ],
  };
}

describe("project validation", () => {
  it("accepts a bounded canonical project", () => {
    expect(isValidProject(project())).toBe(true);
  });

  it("rejects duplicate element ids", () => {
    const value = project();
    value.pages[0]!.root.children.push({
      ...value.pages[0]!.root,
      children: [],
    });
    expect(isValidProject(value)).toBe(false);
  });

  it("rejects route traversal and CSS breakout", () => {
    const value = project();
    value.pages[0]!.route = "../../outside";
    expect(isValidProject(value)).toBe(false);
    expect(isSafeCssPropertyName("color};body{background")).toBe(false);
    expect(isSafeCssValue("red; background: black")).toBe(false);
  });
});
