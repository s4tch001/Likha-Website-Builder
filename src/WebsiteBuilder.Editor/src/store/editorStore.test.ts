import { beforeEach, describe, expect, it } from "vitest";
import type { ElementNode, Project, ProjectAsset } from "../model/types";
import { findNode, useEditorStore } from "./editorStore";
import {
  assetFontFamily,
  editorAssetUrl,
  editorFontFaceCss,
} from "../model/assetElements";

function node(
  id: string,
  x: number,
  y: number,
  w: number,
  h: number,
): ElementNode {
  return {
    id,
    type: "Div",
    x,
    y,
    width: w,
    height: h,
    rotation: 0,
    attributes: {},
    styles: {},
    responsiveStyles: {},
    hidden: false,
    locked: false,
    children: [],
  };
}

function makeProject(children: ElementNode[]): Project {
  return {
    schemaVersion: 2,
    id: "p",
    name: "Test",
    createdUtc: "",
    modifiedUtc: "",
    breakpoints: [
      { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
    ],
    pages: [
      {
        id: "page1",
        name: "Home",
        route: "index",
        root: { ...node("root", 0, 0, 0, 0), children },
      },
    ],
    variables: {},
    assets: [],
  };
}

describe("editorStore", () => {
  beforeEach(() => {
    useEditorStore
      .getState()
      .setProject(
        makeProject([node("a", 0, 0, 100, 40), node("b", 200, 200, 100, 40)]),
      );
  });

  it("rotateElement sets and normalizes the rotation", () => {
    useEditorStore.getState().rotateElement("a", 30);
    expect(findNode(useEditorStore.getState().project!, "a")!.rotation).toBe(
      30,
    );

    // 200° normalizes into (-180, 180].
    useEditorStore.getState().rotateElement("a", 200);
    expect(findNode(useEditorStore.getState().project!, "a")!.rotation).toBe(
      -160,
    );
  });

  it("inserts canonical image, audio, and document assets with semantic attributes", () => {
    const assets: ProjectAsset[] = [
      {
        id: "img",
        name: "Photo",
        storedFileName: "img.png",
        relativePath: "Assets/img.png",
        kind: "Image",
        mediaType: "image/png",
        sizeBytes: 1,
        sha256: "a".repeat(64),
        importedUtc: "2026-01-01T00:00:00Z",
      },
      {
        id: "audio",
        name: "Theme",
        storedFileName: "theme.mp3",
        relativePath: "Assets/theme.mp3",
        kind: "Audio",
        mediaType: "audio/mpeg",
        sizeBytes: 1,
        sha256: "b".repeat(64),
        importedUtc: "2026-01-01T00:00:00Z",
      },
      {
        id: "doc",
        name: "Guide.pdf",
        storedFileName: "guide.pdf",
        relativePath: "Assets/guide.pdf",
        kind: "Document",
        mediaType: "application/pdf",
        sizeBytes: 1,
        sha256: "c".repeat(64),
        importedUtc: "2026-01-01T00:00:00Z",
      },
    ];
    const project = makeProject([]);
    project.assets = assets;
    useEditorStore.getState().setProject(project);

    const store = useEditorStore.getState();
    store.insertAsset(assets[0], 10, 20);
    store.insertAsset(assets[1], 30, 40);
    store.insertAsset(assets[2], 50, 60);
    const children = useEditorStore.getState().project!.pages[0].root.children;
    expect(children.map((child) => child.type)).toEqual([
      "Image",
      "Audio",
      "Link",
    ]);
    expect(children[0].attributes.src).toBe("Assets/img.png");
    expect(children[1].attributes.controls).toBe("true");
    expect(children[2].attributes.download).toBe("Guide.pdf");
    expect(useEditorStore.getState().canUndo).toBe(true);
  });

  it("rejects asset metadata that does not exactly match the current project", () => {
    const asset: ProjectAsset = {
      id: "img",
      name: "Photo",
      storedFileName: "img.png",
      relativePath: "Assets/img.png",
      kind: "Image",
      mediaType: "image/png",
      sizeBytes: 1,
      sha256: "a".repeat(64),
      importedUtc: "2026-01-01T00:00:00Z",
    };
    const project = makeProject([]);
    project.assets = [asset];
    useEditorStore.getState().setProject(project);
    useEditorStore
      .getState()
      .insertAsset({ ...asset, relativePath: "Assets/other.png" }, 0, 0);
    expect(
      useEditorStore.getState().project!.pages[0].root.children,
    ).toHaveLength(0);
  });

  it("resolves only canonical preview URLs and emits project-scoped font faces", () => {
    const font: ProjectAsset = {
      id: "font-12!3",
      name: "Brand",
      storedFileName: "font file.woff2",
      relativePath: "Assets/font file.woff2",
      kind: "Font",
      mediaType: "font/woff2",
      sizeBytes: 1,
      sha256: "d".repeat(64),
      importedUtc: "2026-01-01T00:00:00Z",
    };
    const project = makeProject([]);
    project.assets = [font];
    expect(editorAssetUrl(project, font.relativePath)).toBe(
      "https://project-assets.local/font%20file.woff2",
    );
    expect(editorAssetUrl(project, "Assets/../secret")).toBeNull();
    expect(assetFontFamily(font)).toBe("LikhaAsset_font123");
    expect(editorFontFaceCss(project)).toContain(
      "font-family:'LikhaAsset_font123'",
    );
  });

  it("inserts a component subtree with fresh ids and requested root position", () => {
    const root = node("template-root", 0, 0, 500, 300);
    root.type = "Section";
    root.children = [node("template-child", 20, 20, 100, 40)];
    const beforeRevision = useEditorStore.getState().revision;

    useEditorStore.getState().insertComponent(root, 123.4, 88.8);

    const state = useEditorStore.getState();
    const inserted = state.project!.pages[0].root.children.at(-1)!;
    expect(inserted.id).not.toBe("template-root");
    expect(inserted.children[0].id).not.toBe("template-child");
    expect(inserted.x).toBe(123);
    expect(inserted.y).toBe(89);
    expect(state.selectedIds).toEqual([inserted.id]);
    expect(state.revision).toBe(beforeRevision + 1);
    expect(state.canUndo).toBe(true);
  });

  it("rejects malformed or asset-dependent component trees", () => {
    const root = node("template-root", 0, 0, 500, 300);
    root.attributes.src = "Assets/not-canonical.png";
    const before = useEditorStore.getState().revision;
    useEditorStore.getState().insertComponent(root, 0, 0);
    expect(useEditorStore.getState().revision).toBe(before);
  });

  it("alignSelection right makes right edges equal", () => {
    const store = useEditorStore.getState();
    store.selectMany(["a", "b"]);
    store.alignSelection("right");

    const project = useEditorStore.getState().project!;
    const a = findNode(project, "a")!;
    const b = findNode(project, "b")!;
    expect(a.x + a.width).toBe(b.x + b.width);
  });

  it("setStyle sets and clears a style", () => {
    const store = useEditorStore.getState();
    store.setStyle("a", "background", "#ff0000");
    expect(
      findNode(useEditorStore.getState().project!, "a")!.styles.background,
    ).toBe("#ff0000");

    store.setStyle("a", "background", "");
    expect(
      findNode(useEditorStore.getState().project!, "a")!.styles.background,
    ).toBeUndefined();
  });

  it("undoes and redoes editor project mutations", () => {
    const store = useEditorStore.getState();
    expect(store.canUndo).toBe(false);
    store.setStyle("a", "background", "#ff0000");
    expect(useEditorStore.getState().canUndo).toBe(true);

    useEditorStore.getState().undo();
    expect(
      findNode(useEditorStore.getState().project!, "a")!.styles.background,
    ).toBeUndefined();
    expect(useEditorStore.getState().canRedo).toBe(true);

    useEditorStore.getState().redo();
    expect(
      findNode(useEditorStore.getState().project!, "a")!.styles.background,
    ).toBe("#ff0000");
  });

  it("clears redo on a new mutation and clears all history on host project load", () => {
    useEditorStore.getState().setStyle("a", "color", "red");
    useEditorStore.getState().undo();
    useEditorStore.getState().setStyle("a", "color", "blue");
    expect(useEditorStore.getState().canRedo).toBe(false);

    useEditorStore.getState().setProject(makeProject([]), 99);
    expect(useEditorStore.getState().canUndo).toBe(false);
    expect(useEditorStore.getState().canRedo).toBe(false);
  });

  it("bounds project history to fifty snapshots", () => {
    for (let x = 1; x <= 55; x += 1) {
      useEditorStore.getState().moveElement("a", x, 0);
    }
    for (let count = 0; count < 50; count += 1) {
      useEditorStore.getState().undo();
    }

    expect(findNode(useEditorStore.getState().project!, "a")!.x).toBe(5);
    expect(useEditorStore.getState().canUndo).toBe(false);
  });

  it("setText sets and clears text", () => {
    const store = useEditorStore.getState();
    store.setText("a", "Hello");
    expect(findNode(useEditorStore.getState().project!, "a")!.text).toBe(
      "Hello",
    );

    store.setText("a", "");
    expect(
      findNode(useEditorStore.getState().project!, "a")!.text,
    ).toBeUndefined();
  });

  it("setGeometry updates only the provided fields", () => {
    const store = useEditorStore.getState();
    store.setGeometry("a", { width: 250 });
    const a = findNode(useEditorStore.getState().project!, "a")!;
    expect(a.width).toBe(250);
    expect(a.x).toBe(0); // unchanged
    expect(a.height).toBe(40); // unchanged
  });

  it("deleteSelection removes selected nodes and clears selection", () => {
    const store = useEditorStore.getState();
    store.selectElement("a");
    store.deleteSelection();

    const state = useEditorStore.getState();
    expect(findNode(state.project!, "a")).toBeNull();
    expect(state.selectedIds).toHaveLength(0);
  });

  it("setHidden toggles the flag and drops the node from the selection", () => {
    const store = useEditorStore.getState();
    store.selectElement("a");
    store.setHidden("a", true);

    const state = useEditorStore.getState();
    expect(findNode(state.project!, "a")!.hidden).toBe(true);
    expect(state.selectedIds).not.toContain("a");

    state.setHidden("a", false);
    expect(findNode(useEditorStore.getState().project!, "a")!.hidden).toBe(
      false,
    );
  });

  it("setLocked toggles the flag without affecting the selection", () => {
    const store = useEditorStore.getState();
    store.selectElement("a");
    store.setLocked("a", true);

    const state = useEditorStore.getState();
    expect(findNode(state.project!, "a")!.locked).toBe(true);
    expect(state.selectedIds).toContain("a");
  });

  it("locked elements resist move/resize/rotate/style/text/geometry edits", () => {
    const store = useEditorStore.getState();
    store.setLocked("a", true);

    const before = structuredClone(
      findNode(useEditorStore.getState().project!, "a"),
    );
    const s = useEditorStore.getState();
    s.moveElement("a", 500, 500);
    s.resizeElement("a", 0, 0, 999, 999);
    s.rotateElement("a", 45);
    s.setStyle("a", "background", "#ff0000");
    s.setText("a", "nope");
    s.setGeometry("a", { width: 777 });

    const after = findNode(useEditorStore.getState().project!, "a")!;
    expect(after.x).toBe(before!.x);
    expect(after.y).toBe(before!.y);
    expect(after.width).toBe(before!.width);
    expect(after.height).toBe(before!.height);
    expect(after.rotation).toBe(before!.rotation);
    expect(after.styles.background).toBeUndefined();
    expect(after.text).toBe(before!.text);
  });

  it("locked elements cannot be deleted, individually or in a selection", () => {
    const store = useEditorStore.getState();
    store.setLocked("a", true);

    store.deleteElement("a");
    expect(findNode(useEditorStore.getState().project!, "a")).not.toBeNull();

    store.selectMany(["a", "b"]);
    store.deleteSelection();
    const state = useEditorStore.getState();
    expect(findNode(state.project!, "a")).not.toBeNull(); // locked, survives
    expect(findNode(state.project!, "b")).toBeNull(); // unlocked, deleted
    expect(state.selectedIds).toEqual(["a"]); // locked stays selected
  });

  it("alignSelection skips locked elements", () => {
    const store = useEditorStore.getState();
    store.setLocked("a", true);
    const ax = findNode(useEditorStore.getState().project!, "a")!.x;

    store.selectMany(["a", "b"]);
    store.alignSelection("right");

    // Only one movable element remains → no-op; locked "a" never moves.
    expect(findNode(useEditorStore.getState().project!, "a")!.x).toBe(ax);
  });

  it("renameElement sets and clears the name (and respects locks)", () => {
    const store = useEditorStore.getState();
    store.renameElement("a", "  Hero  ");
    expect(findNode(useEditorStore.getState().project!, "a")!.name).toBe(
      "Hero",
    );

    store.renameElement("a", "");
    expect(
      findNode(useEditorStore.getState().project!, "a")!.name,
    ).toBeUndefined();

    store.setLocked("a", true);
    store.renameElement("a", "Nope");
    expect(
      findNode(useEditorStore.getState().project!, "a")!.name,
    ).toBeUndefined();
  });

  it("reorderElement reorders siblings and reparents", () => {
    const store = useEditorStore.getState();
    const rootChildren = () =>
      findNode(useEditorStore.getState().project!, "root")!.children.map(
        (c) => c.id,
      );

    // Move "a" to the end (after "b").
    store.reorderElement("a", "root", 2);
    expect(rootChildren()).toEqual(["b", "a"]);

    // Reparent "a" inside "b".
    store.reorderElement("a", "b", 0);
    expect(rootChildren()).toEqual(["b"]);
    expect(
      findNode(useEditorStore.getState().project!, "b")!.children.map(
        (c) => c.id,
      ),
    ).toEqual(["a"]);
  });

  it("reorderElement refuses to drop a node into its own descendant", () => {
    useEditorStore.getState().setProject(
      makeProject([
        {
          ...node("c", 0, 0, 100, 100),
          children: [node("d", 10, 10, 40, 40)],
        },
      ]),
    );
    useEditorStore.getState().reorderElement("c", "d", 0);
    // "c" stays under root; the cycle is rejected.
    expect(
      findNode(useEditorStore.getState().project!, "root")!.children.map(
        (x) => x.id,
      ),
    ).toEqual(["c"]);
  });

  it("groupSelection wraps the selection and preserves positions", () => {
    const store = useEditorStore.getState();
    store.selectMany(["a", "b"]);
    store.groupSelection();

    const project = useEditorStore.getState().project!;
    const root = findNode(project, "root")!;
    expect(root.children).toHaveLength(1);
    const group = root.children[0];
    expect(group.children.map((c) => c.id).sort()).toEqual(["a", "b"]);
    // bbox of a(0,0,100,40) + b(200,200,100,40) → origin 0,0 size 300x240.
    expect(group.x).toBe(0);
    expect(group.y).toBe(0);
    expect(group.width).toBe(300);
    expect(group.height).toBe(240);
    // Members keep their absolute positions relative to the group origin.
    expect(findNode(project, "b")).toMatchObject({ x: 200, y: 200 });
    expect(useEditorStore.getState().selectedIds).toEqual([group.id]);
  });

  it("setStyle writes to the active breakpoint's override layer", () => {
    const proj = makeProject([node("a", 0, 0, 100, 40)]);
    proj.breakpoints = [
      { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
      { id: "mobile", label: "Mobile", maxWidth: 480, isBase: false },
    ];
    const store = useEditorStore.getState();
    store.setProject(proj);

    // At the base breakpoint, edits land in the base style map.
    store.setStyle("a", "color", "black");
    expect(
      findNode(useEditorStore.getState().project!, "a")!.styles.color,
    ).toBe("black");

    // At a non-base breakpoint, edits land in that breakpoint's override layer.
    store.setBreakpoint("mobile");
    store.setStyle("a", "color", "red");
    let a = findNode(useEditorStore.getState().project!, "a")!;
    expect(a.styles.color).toBe("black"); // base untouched
    expect(a.responsiveStyles.mobile.color).toBe("red");

    // Clearing removes the override and drops the now-empty layer.
    store.setStyle("a", "color", "");
    a = findNode(useEditorStore.getState().project!, "a")!;
    expect(a.responsiveStyles.mobile).toBeUndefined();
    expect(a.styles.color).toBe("black");
  });

  it("ungroupElement lifts children back to the group's parent", () => {
    useEditorStore.getState().setProject(
      makeProject([
        {
          ...node("g", 10, 10, 200, 200),
          children: [node("e", 5, 5, 40, 40)],
        },
      ]),
    );
    useEditorStore.getState().ungroupElement("g");

    const project = useEditorStore.getState().project!;
    expect(findNode(project, "g")).toBeNull();
    // e's coords convert to parent space: 10+5, 10+5.
    expect(findNode(project, "e")).toMatchObject({ x: 15, y: 15 });
    expect(useEditorStore.getState().selectedIds).toEqual(["e"]);
  });
});
