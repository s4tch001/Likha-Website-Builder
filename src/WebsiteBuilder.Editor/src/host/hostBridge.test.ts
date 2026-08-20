import { beforeEach, describe, expect, it, vi } from "vitest";
import type { EventListener, RequestHandler } from "../bridge/types";
import type { ElementNode, Project } from "../model/types";
import { findNode, useEditorStore } from "../store/editorStore";

const harness = vi.hoisted(() => ({
  handlers: new Map<string, RequestHandler>(),
  listeners: new Map<string, EventListener[]>(),
  published: [] as Array<{ method: string; payload: unknown }>,
  invoke: vi.fn(),
}));

vi.mock("../bridge/bridge", () => ({
  bridge: {
    isHosted: true,
    handle: (method: string, handler: RequestHandler) =>
      harness.handlers.set(method, handler),
    on: (method: string, listener: EventListener) => {
      const listeners = harness.listeners.get(method) ?? [];
      listeners.push(listener);
      harness.listeners.set(method, listeners);
      return () => undefined;
    },
    publish: (method: string, payload: unknown) =>
      harness.published.push({ method, payload }),
    invoke: harness.invoke,
  },
}));

function element(id: string): ElementNode {
  return {
    id,
    type: "Div",
    x: 0,
    y: 0,
    width: 100,
    height: 40,
    rotation: 0,
    attributes: {},
    styles: {},
    responsiveStyles: {},
    hidden: false,
    locked: false,
    children: [],
  };
}

function project(name = "Host project"): Project {
  return {
    schemaVersion: 2,
    id: "project-1",
    name,
    createdUtc: "2026-08-20T00:00:00Z",
    modifiedUtc: "2026-08-20T00:00:00Z",
    breakpoints: [
      { id: "desktop", label: "Desktop", maxWidth: 0, isBase: true },
      { id: "mobile", label: "Mobile", maxWidth: 480, isBase: false },
    ],
    variables: {},
    assets: [],
    pages: [
      {
        id: "page-1",
        name: "Home",
        route: "index",
        root: {
          ...element("root-1"),
          type: "Section",
          children: [element("a"), element("b")],
        },
      },
    ],
  };
}

function emit(method: string, payload?: unknown): void {
  for (const listener of harness.listeners.get(method) ?? []) {
    listener(payload);
  }
}

beforeEach(() => {
  harness.handlers.clear();
  harness.listeners.clear();
  harness.published.length = 0;
  harness.invoke.mockReset();
  useEditorStore.setState({
    project: null,
    hostRevision: 0,
    revision: 0,
    activePageId: "",
    breakpointId: "",
    selectedIds: [],
    zoom: 100,
    ready: false,
  });
});

describe("connectHost", () => {
  it("handshakes, validates host snapshots, and wires host commands", async () => {
    vi.useFakeTimers();
    const initial = project();
    harness.invoke.mockImplementation((method: string) => {
      if (method === "host.getProject") {
        return Promise.resolve({ project: initial, revision: 7 });
      }
      if (method === "host.getInfo") {
        return Promise.resolve({
          name: "Likha",
          version: "0.1.0",
          platform: "Windows",
        });
      }
      return Promise.resolve({ accepted: true, revision: 10 });
    });

    const { connectHost } = await import("./hostBridge");
    await expect(connectHost()).resolves.toEqual({
      name: "Likha",
      version: "0.1.0",
      platform: "Windows",
    });
    expect(useEditorStore.getState()).toMatchObject({
      project: initial,
      hostRevision: 7,
      ready: true,
    });
    expect(harness.published).toContainEqual({
      method: "editor.ready",
      payload: { editor: "WebsiteBuilder", version: "0.1.0" },
    });

    expect(
      await harness.handlers.get("editor.echo")?.({ message: "ping" }),
    ).toEqual({
      reply: 'editor received "ping"',
    });

    emit("project.load", { project: { unsafe: true }, revision: 9 });
    expect(useEditorStore.getState().project?.name).toBe("Host project");
    emit("project.load", { project: project("Authoritative"), revision: 9 });
    expect(useEditorStore.getState().project?.name).toBe("Authoritative");

    emit("editor.setZoom", { zoom: 125 });
    emit("editor.setBreakpoint", { id: "mobile" });
    emit("editor.select", { ids: ["a", "b"] });
    emit("editor.align", { mode: "right" });
    emit("editor.rename", { id: "a", name: "Hero" });
    emit("editor.setGeometry", { id: "a", width: 220 });
    emit("editor.setRotation", { id: "a", deg: 15 });
    emit("editor.setText", { id: "a", text: "Hello" });
    emit("editor.setStyle", { id: "a", name: "color", value: "red" });
    emit("editor.setHidden", { id: "b", value: true });
    emit("editor.setLocked", { id: "b", value: true });

    const state = useEditorStore.getState();
    expect(state.zoom).toBe(125);
    expect(state.breakpointId).toBe("mobile");
    expect(findNode(state.project!, "a")).toMatchObject({
      name: "Hero",
      width: 220,
      rotation: 15,
      text: "Hello",
    });
    expect(findNode(state.project!, "a")?.responsiveStyles.mobile.color).toBe(
      "red",
    );
    expect(findNode(state.project!, "b")).toMatchObject({
      hidden: true,
      locked: true,
    });
    expect(state.canUndo).toBe(true);
    expect(harness.published).toContainEqual({
      method: "editor.historyChanged",
      payload: expect.objectContaining({ canUndo: true }),
    });

    emit("editor.undo");
    expect(findNode(useEditorStore.getState().project!, "b")?.locked).toBe(
      false,
    );
    emit("editor.redo");
    expect(findNode(useEditorStore.getState().project!, "b")?.locked).toBe(
      true,
    );

    useEditorStore.getState().renameElement("a", "Edited again");
    await vi.advanceTimersByTimeAsync(100);
    expect(harness.invoke).toHaveBeenCalledWith(
      "host.applyProjectUpdate",
      expect.objectContaining({ baseRevision: 9 }),
    );
    expect(useEditorStore.getState().hostRevision).toBe(10);
    vi.useRealTimers();
  });
});
