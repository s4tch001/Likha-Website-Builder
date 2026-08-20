import { bridge } from "../bridge/bridge";
import type { Project } from "../model/types";
import { isValidElementTree, isValidProject } from "../model/projectValidation";
import { effectiveStyles } from "../model/responsive";
import {
  type AlignMode,
  findNode,
  findParent,
  useEditorStore,
} from "../store/editorStore";

export interface HostInfo {
  name: string;
  version: string;
  platform: string;
}

interface ProjectSyncEnvelope {
  project: Project;
  revision: number;
}

interface ProjectUpdateResponse {
  accepted: boolean;
  revision: number;
  project?: Project | null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readProjectEnvelope(value: unknown): ProjectSyncEnvelope | null {
  if (
    !isRecord(value) ||
    !Number.isSafeInteger(value.revision) ||
    !isValidProject(value.project)
  ) {
    return null;
  }

  return { project: value.project, revision: value.revision as number };
}

let connected = false;

/**
 * Connects the editor to the WPF host: registers handlers/listeners, performs the
 * handshake and keeps the host's zoom display in sync. Returns the resolved host
 * info (or null when running standalone in a browser). Safe to call once.
 */
export async function connectHost(): Promise<HostInfo | null> {
  if (connected) {
    return null;
  }
  connected = true;

  const store = useEditorStore.getState();

  // Host → editor request (proves the reverse direction during handshake).
  bridge.handle("editor.echo", (payload) => {
    const message = (payload as { message?: string })?.message ?? "";
    return { reply: `editor received "${message}"` };
  });

  // Host → editor events.
  bridge.on("project.load", (payload) => {
    const snapshot = readProjectEnvelope(payload);
    if (snapshot) {
      useEditorStore.getState().setProject(snapshot.project, snapshot.revision);
    }
  });
  bridge.on("editor.setZoom", (payload) => {
    const zoom = (payload as { zoom?: number })?.zoom;
    if (typeof zoom === "number") {
      useEditorStore.getState().setZoom(zoom);
    }
  });
  bridge.on("editor.setBreakpoint", (payload) => {
    const id = (payload as { id?: string })?.id;
    if (typeof id === "string") {
      useEditorStore.getState().setBreakpoint(id);
    }
  });

  // Host → editor: Edit-ribbon actions operating on the current selection.
  bridge.on("editor.deleteSelected", () => {
    useEditorStore.getState().deleteSelection();
  });
  bridge.on("editor.duplicateSelected", () => {
    useEditorStore.getState().duplicateSelection();
  });
  bridge.on("editor.align", (payload) => {
    const mode = (payload as { mode?: AlignMode })?.mode;
    if (mode) {
      useEditorStore.getState().alignSelection(mode);
    }
  });

  // Host → editor: Layers-panel selection sync. Selects one or many elements on
  // the canvas (empty/missing clears the selection).
  bridge.on("editor.select", (payload) => {
    const p = payload as { id?: string | null; ids?: string[] };
    if (Array.isArray(p?.ids)) {
      useEditorStore.getState().selectMany(p.ids);
    } else {
      useEditorStore.getState().selectElement(p?.id ?? null);
    }
  });

  // Host → editor: Layers-panel rename + drag-reorder.
  bridge.on("editor.rename", (payload) => {
    const p = payload as { id?: string; name?: string };
    if (p?.id) {
      useEditorStore.getState().renameElement(p.id, p.name ?? "");
    }
  });
  bridge.on("editor.reorder", (payload) => {
    const p = payload as { id?: string; parentId?: string; index?: number };
    if (p?.id && p.parentId && typeof p.index === "number") {
      useEditorStore.getState().reorderElement(p.id, p.parentId, p.index);
    }
  });

  // Host → editor: group / ungroup the current selection.
  bridge.on("editor.group", () => {
    useEditorStore.getState().groupSelection();
  });
  bridge.on("editor.ungroup", (payload) => {
    const p = payload as { id?: string };
    const id = p?.id ?? useEditorStore.getState().selectedIds[0];
    if (id) {
      useEditorStore.getState().ungroupElement(id);
    }
  });

  // Host → editor: Layers-panel hide / lock toggles.
  bridge.on("editor.setHidden", (payload) => {
    const p = payload as { id?: string; value?: boolean };
    if (p?.id) {
      useEditorStore.getState().setHidden(p.id, p.value ?? false);
    }
  });
  bridge.on("editor.setLocked", (payload) => {
    const p = payload as { id?: string; value?: boolean };
    if (p?.id) {
      useEditorStore.getState().setLocked(p.id, p.value ?? false);
    }
  });

  // Host → editor: Property Inspector edits.
  bridge.on("editor.setStyle", (payload) => {
    const p = payload as { id?: string; name?: string; value?: string };
    if (p?.id && p.name) {
      useEditorStore.getState().setStyle(p.id, p.name, p.value ?? "");
    }
  });
  bridge.on("editor.setGeometry", (payload) => {
    const p = payload as {
      id?: string;
      x?: number;
      y?: number;
      width?: number;
      height?: number;
    };
    if (p?.id) {
      useEditorStore.getState().setGeometry(p.id, {
        x: p.x,
        y: p.y,
        width: p.width,
        height: p.height,
      });
    }
  });
  bridge.on("editor.setRotation", (payload) => {
    const p = payload as { id?: string; deg?: number };
    if (p?.id && typeof p.deg === "number") {
      useEditorStore.getState().rotateElement(p.id, p.deg);
    }
  });
  bridge.on("editor.setText", (payload) => {
    const p = payload as { id?: string; text?: string };
    if (p?.id) {
      useEditorStore.getState().setText(p.id, p.text ?? "");
    }
  });

  // Host → editor: insert an element from the WPF Components panel. Placed near
  // the top-left of the page with a small stagger so repeated inserts don't stack.
  bridge.on("editor.insertElement", (payload) => {
    const type = (payload as { type?: string })?.type;
    if (typeof type === "string") {
      const n = useEditorStore.getState().revision % 6;
      useEditorStore.getState().insertElement(type, 80 + n * 24, 80 + n * 24);
    }
  });

  bridge.on("editor.insertAsset", (payload) => {
    const asset = (payload as { asset?: unknown })?.asset;
    if (!asset || typeof asset !== "object") {
      return;
    }
    const candidate = asset as import("../model/types").ProjectAsset;
    const state = useEditorStore.getState();
    const canonical = state.project?.assets.find(
      (item) =>
        item.id === candidate.id &&
        item.storedFileName === candidate.storedFileName &&
        item.relativePath === candidate.relativePath &&
        item.kind === candidate.kind &&
        item.mediaType === candidate.mediaType,
    );
    if (canonical) {
      const n = state.revision % 6;
      state.insertAsset(canonical, 80 + n * 24, 80 + n * 24);
    }
  });

  bridge.on("editor.insertComponent", (payload) => {
    const root = (payload as { root?: unknown })?.root;
    if (!isValidElementTree(root)) {
      return;
    }
    const state = useEditorStore.getState();
    const n = state.revision % 6;
    state.insertComponent(root, 64 + n * 24, 64 + n * 24);
  });
  bridge.on("editor.undo", () => useEditorStore.getState().undo());
  bridge.on("editor.redo", () => useEditorStore.getState().redo());

  // Headless verification: drive a move + reparent and report the outcome.
  bridge.on("editor.runSelfTest", () => {
    const s = useEditorStore.getState();
    const project = s.project;
    if (!project) {
      return;
    }
    const button = findNode(project, "hero-button");
    if (button) {
      s.moveElement("hero-button", button.x + 60, button.y + 20);
    }
    s.reparentElement("hero-button", "feature-card", 24, 120);

    const after = useEditorStore.getState().project;
    const parent = after ? findParent(after, "hero-button") : null;
    const node = after ? findNode(after, "hero-button") : null;
    bridge.publish("editor.selftestResult", {
      id: "hero-button",
      parentId: parent ? parent.id : null,
      x: node ? node.x : null,
      y: node ? node.y : null,
    });

    // Exercise selection + resize + duplicate so the host Property Inspector and
    // Edit commands can be verified.
    const s2 = useEditorStore.getState();
    const card = after ? findNode(after, "feature-card") : null;
    if (card) {
      s2.resizeElement(
        "feature-card",
        card.x,
        card.y,
        card.width + 80,
        card.height + 40,
      );
    }
    s2.selectElement("feature-card");
    s2.duplicateElement("feature-card");

    // Exercise multi-selection + alignment: align right, then report both right
    // edges (they should be equal afterwards).
    s2.selectMany(["hero-heading", "hero-subtitle"]);
    s2.alignSelection("right");

    const aligned = useEditorStore.getState().project;
    const heading = aligned ? findNode(aligned, "hero-heading") : null;
    const subtitle = aligned ? findNode(aligned, "hero-subtitle") : null;
    bridge.publish("editor.alignResult", {
      headingRight: heading ? heading.x + heading.width : null,
      subtitleRight: subtitle ? subtitle.x + subtitle.width : null,
    });

    // Rotate an element and report the stored angle.
    s2.selectElement("feature-card");
    s2.rotateElement("feature-card", 30);
    const rotated = useEditorStore.getState().project;
    const card2 = rotated ? findNode(rotated, "feature-card") : null;
    bridge.publish("editor.rotateResult", {
      rotation: card2 ? card2.rotation : null,
    });

    // Demonstrate the configurable canvas background (without persisting it).
    useEditorStore.setState({ canvasBackground: "#0c2a3a" });

    // Set advanced + custom styles so the inspector's ADVANCED and CUSTOM CSS
    // sections have content, then leave the card selected.
    const sx = useEditorStore.getState();
    sx.setStyle("feature-card", "overflow", "hidden");
    sx.setStyle("feature-card", "cursor", "pointer");
    sx.setStyle("feature-card", "--brand", "#2563eb");

    // Demonstrate rename (8b) and group (8c) so the Layers panel shows both a
    // renamed node and a "Group" container in screenshots.
    sx.renameElement("feature-card", "Main Card");
    sx.selectMany(["hero-heading", "hero-subtitle"]);
    sx.groupSelection();

    // Lock the renamed card so the locked-state UI is exercised in the Layers tree.
    sx.setLocked("feature-card", true);

    // Demonstrate the responsive WRITE path (9b): switch to a narrow breakpoint and
    // override the heading's colour + size there. The base (Desktop) stays unchanged;
    // the canvas + inspector reflect the Mobile overrides via the cascade.
    sx.selectElement("hero-heading");
    sx.setBreakpoint("mobile");
    const sb = useEditorStore.getState();
    sb.setStyle("hero-heading", "color", "#ff5566");
    sb.setStyle("hero-heading", "font-size", "40px");
  });

  // Standalone/benchmark mode has no transport consumers. Avoid installing
  // host-sync subscriptions whose timers and derived payloads would measure
  // bridge bookkeeping instead of editor interaction work.
  if (!bridge.isHosted) {
    store.setReady(true);
    return null;
  }

  // Push editor-originated model edits through a revision-checked host request.
  // This prevents a stale full-project snapshot from silently overwriting a
  // host-originated asset mutation. Only one update is in flight at a time.
  let lastRevision = store.revision;
  let pushTimer: ReturnType<typeof setTimeout> | null = null;
  let pushInFlight = false;
  let pushAgain = false;

  const pushProject = async (): Promise<void> => {
    if (pushInFlight) {
      pushAgain = true;
      return;
    }

    const state = useEditorStore.getState();
    if (!state.project || !bridge.isHosted) {
      return;
    }

    pushInFlight = true;
    const sentRevision = state.revision;
    try {
      const response = await bridge.invoke<ProjectUpdateResponse>(
        "host.applyProjectUpdate",
        {
          baseRevision: state.hostRevision,
          project: state.project,
        },
      );
      if (response.accepted) {
        useEditorStore.getState().acknowledgeHostRevision(response.revision);
      } else if (response.project) {
        useEditorStore
          .getState()
          .setProject(response.project, response.revision);
      }
    } catch {
      // Keep the local edit in memory. A later mutation will retry; the host
      // revision check prevents this snapshot from being accepted if now stale.
    } finally {
      pushInFlight = false;
      const changedWhileSending =
        useEditorStore.getState().revision !== sentRevision;
      if (pushAgain || changedWhileSending) {
        pushAgain = false;
        void pushProject();
      }
    }
  };

  useEditorStore.subscribe((state) => {
    if (state.revision !== lastRevision) {
      lastRevision = state.revision;
      if (pushTimer) {
        clearTimeout(pushTimer);
      }
      pushTimer = setTimeout(() => {
        void pushProject();
      }, 90);
    }
  });

  let lastCanUndo = store.canUndo;
  let lastCanRedo = store.canRedo;
  const publishHistory = () => {
    const state = useEditorStore.getState();
    bridge.publish("editor.historyChanged", {
      canUndo: state.canUndo,
      canRedo: state.canRedo,
    });
  };
  useEditorStore.subscribe((state) => {
    if (state.canUndo !== lastCanUndo || state.canRedo !== lastCanRedo) {
      lastCanUndo = state.canUndo;
      lastCanRedo = state.canRedo;
      publishHistory();
    }
  });

  // Publish the selection to the host so the Property Inspector reflects the
  // canvas selection. Sends the count plus the primary element (first selected).
  let lastSelKey = store.selectedIds.join(",");
  let lastSelRevision = store.revision;
  const publishSelection = () => {
    const state = useEditorStore.getState();
    const primaryId = state.selectedIds[0];
    const element =
      primaryId && state.project ? findNode(state.project, primaryId) : null;
    // Include the active breakpoint and the resolved (cascaded) styles so the host
    // Property Inspector shows effective values for the breakpoint being edited.
    const breakpoints = state.project?.breakpoints ?? [];
    const active = breakpoints.find((b) => b.id === state.breakpointId);
    const isBase = !active || active.isBase;
    const effective = element
      ? effectiveStyles(element, breakpoints, state.breakpointId)
      : null;
    // Keys overridden specifically at the active (non-base) breakpoint.
    const overridden =
      element && active && !active.isBase
        ? Object.keys(element.responsiveStyles?.[active.id] ?? {})
        : [];
    bridge.publish("editor.selectionChanged", {
      ids: state.selectedIds,
      element,
      breakpointId: state.breakpointId,
      breakpointLabel: active?.label ?? null,
      isBaseBreakpoint: isBase,
      effective,
      overridden,
    });
  };
  useEditorStore.subscribe((state) => {
    const key = state.selectedIds.join(",");
    if (
      key !== lastSelKey ||
      (state.selectedIds.length > 0 && state.revision !== lastSelRevision)
    ) {
      lastSelKey = key;
      lastSelRevision = state.revision;
      publishSelection();
    }
  });

  // Re-publish the selection when the breakpoint changes so the inspector refreshes
  // to the effective values (and override state) of the newly active breakpoint.
  let lastBp = store.breakpointId;
  useEditorStore.subscribe((state) => {
    if (state.breakpointId !== lastBp) {
      lastBp = state.breakpointId;
      if (state.selectedIds.length > 0) {
        publishSelection();
      }
    }
  });

  // Whenever zoom changes (wheel, or a host-driven set), tell the host so its
  // toolbar percentage stays in sync. The host never echoes this back, so there
  // is no feedback loop.
  let lastZoom = store.zoom;
  useEditorStore.subscribe((state) => {
    if (state.zoom !== lastZoom) {
      lastZoom = state.zoom;
      bridge.publish("editor.viewChanged", { zoom: state.zoom });
    }
  });

  store.setReady(true);

  // Announce readiness, then pull the current project.
  bridge.publish("editor.ready", {
    editor: "WebsiteBuilder",
    version: "0.1.0",
  });
  publishHistory();

  // The host's request handler returns the project as a JSON object payload.
  const snapshot = readProjectEnvelope(
    await bridge.invoke<unknown>("host.getProject"),
  );
  if (snapshot) {
    useEditorStore.getState().setProject(snapshot.project, snapshot.revision);
  }

  return bridge.invoke<HostInfo>("host.getInfo");
}
