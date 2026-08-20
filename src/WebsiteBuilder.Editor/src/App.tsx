"use client";

import { useEffect, useState } from "react";
import { bridge } from "./bridge/bridge";
import AlignToolbar from "./canvas/AlignToolbar";
import Canvas from "./canvas/Canvas";
import { connectHost, type HostInfo } from "./host/hostBridge";
import type { ElementNode } from "./model/types";
import { createBenchmarkProject } from "./model/benchmarkProject";
import ComponentPalette from "./palette/ComponentPalette";
import { useActiveBreakpoint, useEditorStore } from "./store/editorStore";

function countNodes(node: ElementNode): number {
  return 1 + node.children.reduce((sum, child) => sum + countNodes(child), 0);
}

/**
 * Phase 4 editor: a thin status bar over the infinite design canvas. The canvas
 * renders the active page from the Project JSON delivered by the host and keeps
 * its zoom/breakpoint in sync over the bridge. Drag-and-drop, selection and
 * property editing arrive in Phases 5+.
 */
export default function App() {
  const [hostInfo, setHostInfo] = useState<HostInfo | null>(null);
  const [error, setError] = useState<string | null>(null);

  const project = useEditorStore((s) => s.project);
  const zoom = useEditorStore((s) => s.zoom);
  const canvasBackground = useEditorStore((s) => s.canvasBackground);
  const setCanvasBackground = useEditorStore((s) => s.setCanvasBackground);
  const breakpoint = useActiveBreakpoint();

  useEffect(() => {
    if (!bridge.isHosted) {
      const requested = Number(
        new URLSearchParams(window.location.search).get("benchmark"),
      );
      if (Number.isFinite(requested) && requested > 0) {
        useEditorStore.getState().setProject(createBenchmarkProject(requested));
      }
    }
    connectHost()
      .then(setHostInfo)
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : String(err)),
      );
  }, []);

  // Report a render summary to the host once a project is loaded (diagnostics).
  useEffect(() => {
    if (project) {
      const elements = project.pages.reduce(
        (sum, p) => sum + countNodes(p.root),
        0,
      );
      bridge.publish("editor.rendered", {
        project: project.name,
        pages: project.pages.length,
        elements,
      });
    }
  }, [project]);

  const status = !bridge.isHosted
    ? "Standalone (browser)"
    : hostInfo
      ? `Connected · ${hostInfo.name} ${hostInfo.version}`
      : error
        ? `Bridge error: ${error}`
        : "Handshaking…";

  return (
    <div className="editor-root">
      <header className="editor-topbar">
        <span className="topbar-title">{project?.name ?? "Likha"}</span>
        <span
          className={`topbar-status${bridge.isHosted && hostInfo ? " ok" : ""}`}
          role="status"
          aria-live="polite"
        >
          {status}
        </span>
        <label className="topbar-bg" title="Canvas background colour">
          <span>Canvas</span>
          <input
            type="color"
            value={canvasBackground}
            onChange={(e) => setCanvasBackground(e.target.value)}
          />
        </label>
        <span className="topbar-meta">
          {breakpoint ? `${breakpoint.label} · ` : ""}
          {Math.round(zoom)}%
        </span>
      </header>
      <main className="editor-body" aria-label="Website editor workspace">
        <ComponentPalette />
        <div className="editor-canvas-host">
          <Canvas />
          <AlignToolbar />
        </div>
      </main>
    </div>
  );
}
