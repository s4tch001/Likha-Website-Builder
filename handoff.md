# Likha - Website Builder — Project Handoff

> Handoff document for continuing this project in a new session. Read this fully
> before starting. It captures the architecture, conventions, what is DONE, what
> remains, and the practical gotchas learned along the way.

---

## 0. What this is

**Likha - Website Builder** — a professional Windows desktop **visual website
builder** (Webflow/Framer-class). Locked stack:

- **Host:** C# / .NET 8 / **WPF** (Windows-only), hosting the editor in **WebView2**.
- **Editor:** **Next.js 16.3.0 App Router + React 19.2.8 + TypeScript 7.0.2 + Zustand**,
  statically exported and loaded inside WebView2.
- **Model:** a canonical **Project JSON** is the single source of truth. The editor
  mutates it; code generators (later phases) emit **clean HTML5/CSS3/JS and React**
  from it. HTML is never edited directly.
- Project root: `D:\p-website-builder`

### Locked technical decisions (do not change without asking the user)
- WPF↔Web bridge: **typed JSON-RPC over WebView2 `postMessage`** (`IEditorBridge`).
- WPF foundation: **CommunityToolkit.Mvvm** + **Microsoft.Extensions DI/Hosting**.
- Docking: **AvalonDock (Dirkster fork)** 4.72.0 + VS2013 dark theme.

### ⚠️ WORKING STYLE — the user's standing rule
**Split EVERY build phase into smaller sub-phases (e.g. 8a, 8b, 8c) and deliver ONE
sub-phase per turn**, pausing for confirmation between them. The user is mindful of
their Claude usage limit. Do NOT attempt a whole large phase in a single turn.
At the start of a phase, break it into a few coherent, independently-verifiable
chunks; implement + verify ONE chunk; end by stating what the next sub-phase covers.
The user often writes in **Filipino/Tagalog**; reply in kind.

---

## 1. Solution structure

```
WebsiteBuilder.sln
├── src/
│   ├── WebsiteBuilder.App        WPF shell (net8.0-windows). AssemblyName=WebsiteBuilder (exe is WebsiteBuilder.exe — INTERNAL name kept; display name is "Likha - Website Builder")
│   ├── WebsiteBuilder.Core       Domain model (Project/Page/ElementNode/Breakpoint), services, ProjectSerializer. No UI deps.
│   ├── WebsiteBuilder.Bridge     JSON-RPC contract: IEditorBridge, BridgeMessage{id,type,method,payload,error}
│   ├── WebsiteBuilder.CodeGen    ICodeGenerator + working static HTML and React emitters
│   └── WebsiteBuilder.Editor     Next.js/React/TS/Zustand editor. Static export → ../WebsiteBuilder.App/wwwroot
└── tests/
    ├── WebsiteBuilder.Core.Tests      (xUnit) 30 tests
    └── WebsiteBuilder.CodeGen.Tests   (xUnit) 20 tests
```

Editor entry/config (`src/WebsiteBuilder.Editor/`):
- `app/layout.tsx`, `app/page.tsx`, `app/error.tsx` — App Router shell, metadata,
  production CSP, and the route error boundary.
- `next.config.ts` — `output: "export"`; `scripts/copy-static-export.mjs` safely replaces
  the WPF source `wwwroot` with the generated `out` directory.

Editor source (`src/WebsiteBuilder.Editor/src/`):
- `model/types.ts` — TS mirror of the C# Project JSON (camelCase). `elementFactory.ts`.
- `store/editorStore.ts` — Zustand store: project, selection (selectedIds[]), zoom/pan,
  breakpoint, canvasBackground, and ALL mutations (insert/move/resize/rotate/reparent/
  delete/duplicate/align/setStyle/setText/setGeometry…). `editorStore.test.ts`, `snap.test.ts` (Vitest).
- `bridge/bridge.ts` + `bridge/types.ts` — TS JSON-RPC client (mirror of C# bridge).
- `host/hostBridge.ts` — `connectHost()` wires bridge↔store; also the **WB_SELFTEST hook**.
- `canvas/` — Canvas.tsx (infinite canvas, zoom/pan, select, drag, marquee, snap),
  ElementRenderer.tsx, SelectionOverlay.tsx (resize + rotate handles), Rulers.tsx,
  snap.ts (smart guides), CanvasContext.ts.
- `palette/` — ComponentPalette (drag source), catalog.ts.

---

## 2. Build / run / test (IMPORTANT — read the gotchas)

`dotnet` is **not on this shell's PATH**. Use the full path:
```
"C:\Program Files\dotnet\dotnet.exe"
```
.NET 8 SDK 8.0.422 is installed. Node 24 + npm. WebView2 Runtime 146 present.

### Build & test
```sh
# Editor (from src/WebsiteBuilder.Editor):
npm install            # first time
npm run build          # next build (static export) → copies out/ to ../WebsiteBuilder.App/wwwroot
npm run typecheck      # tsc --noEmit
npm test               # vitest run (currently 26 tests pass)

# C# (from repo root):
"C:\Program Files\dotnet\dotnet.exe" build WebsiteBuilder.sln
"C:\Program Files\dotnet\dotnet.exe" test  WebsiteBuilder.sln   # 50 tests pass
"C:\Program Files\dotnet\dotnet.exe" run --project src/WebsiteBuilder.App
```

### 🔴 CRITICAL GOTCHA: after editing the React editor, you MUST `dotnet build` the App
`npm run build` creates a Next.js static export in `out/`, then the guarded copy script
replaces the **source** `wwwroot/`. The running exe loads its **own
copy** in `bin/Debug/net8.0-windows/wwwroot/`, copied by MSBuild (Content,
PreserveNewest) only during `dotnet build`. So the sequence is always:
`npm run build` (editor) → `dotnet build` (app) → run the exe. Running the exe after
only `npm run build` uses a STALE editor bundle. (This bit us once.)

### Warnings-as-errors
`TreatWarningsAsErrors=true` solution-wide. Builds must be 0 warnings / 0 errors.

---

## 3. The bridge (how WPF and the editor talk)

Symmetric JSON-RPC. Both sides send `BridgeMessage{ id, type:"Request"|"Response"|"Event", method, payload, error? }`.

- C#: `WebView2EditorBridge` (App/Bridge) implements `IEditorBridge`. `EditorSession`
  (App/Services, DI singleton) owns the WebView2 lifecycle: creates the runtime,
  maps virtual host `editor.local` → output `wwwroot`, navigates
  `https://editor.local/index.html`, registers host handlers, drives the handshake.
- TS: `bridge.ts` mirror (`invoke`/`publish`/`handle`/`on`).

### Data-flow rules (CRITICAL to keep consistent)
- Editor mutations bump a `revision` counter → `hostBridge` pushes
  `editor.projectChanged` (debounced 90ms) → host `IProjectService.ApplyEditorUpdate`
  raises **`Mutated`** (NOT `CurrentChanged`) so it is NOT echoed back to the editor.
- Selection: editor publishes `editor.selectionChanged {ids, element}` →
  `EditorSession.SelectionChanged` → Property Inspector loads it.
- Host→editor edits (e.g. Property Inspector): `editor.setStyle/setGeometry/setRotation/
  setText/insertElement/deleteSelected/duplicateSelected/align/setZoom/setBreakpoint`.
- Host handler string-returns are PARSED to JS objects by the C# bridge (so
  `host.getProject` resolves to a project OBJECT on the TS side, not a string).

### Debug / verification helpers (use these to verify headlessly)
- Env `WB_SELFTEST=1` → after the handshake, `EditorSession` publishes `editor.runSelfTest`;
  the editor (`hostBridge.ts` runSelfTest) drives a scripted sequence (insert, reparent,
  resize, duplicate, multi-select, align, rotate, set styles, select an element) and
  publishes result events. **Use/extend this to leave a specific element selected or
  styles applied for screenshots.** It currently ends by setting overflow/cursor/--brand
  on feature-card and selecting it.
- Env `WB_TRACE_EDITOR=<path>` → `EditorSession.SetStatus` writes status lines to a file
  (handshake, selection, edits). Read it to verify flows.
- Env `WB_TRACE_BINDINGS=<path>` → App writes WPF data-binding warnings to a file
  (clean = no binding errors). Marker line `[binding-trace started]` confirms it attached.
- Env `WB_EDITOR_DEVSERVER=1` (+ `WB_EDITOR_DEVURL`) → load the Next.js dev server
  (`http://127.0.0.1:3000` by default) instead of the bundle.

### Headless screenshot pattern (Windows, no Start-menu entry for the app)
The app isn't a Start-menu app, so computer-use `request_access` can't find it. Capture
via PowerShell + Win32: launch exe, `SetWindowPos`/`SetForegroundWindow`, **guard with
`GetForegroundWindow()==hwnd` before capturing** (so you never capture the user's other
windows), `Graphics.CopyFromScreen`, save PNG, then `Read` it. To reveal panel content
below the fold, position the cursor over the panel and send `mouse_event(0x0800,...,-120,..)`
wheel events. The exe icon can be verified with `Icon.ExtractAssociatedIcon`; the live
window icon via `SendMessage(hwnd, WM_GETICON=0x7F, ICON_BIG=1, 0)`.

---

## 4. Key conventions / patterns already established

- **Property Inspector two-way editing** (App/ViewModels/Panels/PropertyInspectorViewModel.cs):
  `_suppressPush` guard during `Load(node)` prevents feedback loops; `[ObservableProperty]`
  setters push via `EditorSession.SetStyle/SetGeometry/SetText/SetRotation`. Fields use
  `UpdateSourceTrigger=LostFocus` + an attached behavior `TextBoxBehavior.UpdateSourceOnEnter`
  (Behaviors/) so **Enter applies immediately**. Color swatches open a WinForms `ColorDialog`
  (`UseWindowsForms=true`; implicit `System.Windows.Forms`/`System.Drawing` global usings
  are removed via `<Using Remove=.../>` to avoid clashing with WPF; manifest DPI block was
  removed to satisfy WinForms analyzer WFAC010 — WPF .NET8 is PerMonitorV2 by default).
- **Dark ComboBox**: `DarkComboBox`/`DarkComboBoxItem` styles in `Themes/Controls.xaml`.
- **Commands**: a single `ICommandRegistry`/`AppCommand` drives BOTH the ribbon AND the
  Ctrl+Shift+P command palette. Ribbon XAML binds via indexer `{Binding Registry[id].Command}`.
  View-only layout ops go through `IShellLayout` (implemented by MainWindow).
- **Layout persistence + WebView2 airspace**: MainWindow saves/restores AvalonDock layout
  (`XmlLayoutSerializer` → `%LOCALAPPDATA%\WebsiteBuilder\layout.xml` on Window.Closing;
  content reconnected by ContentId via LayoutSerializationCallback). Auto-hide flyouts hide
  the WebView2 (`LayoutAutoHideWindowControl.IsVisibleChanged`) so the native canvas doesn't
  draw over them. NOTE: a graceful close (`$proc.CloseMainWindow()`) is needed for the layout
  to save; `Stop-Process -Force` skips Window.Closing.
- **App identity**: `SetCurrentProcessExplicitAppUserModelID("Likha.WebsiteBuilder")` in
  App ctor so the taskbar uses the window icon (Likha logo at `Assets/likha.ico/.png`).
- **CSS variables / XML comments**: a `--` sequence is illegal inside XML/XAML comments
  (build error MC3000) — don't put CSS var names like `--brand` in comments.
- **Embedded editor security**: the production export has a restrictive CSP; WebView2
  DevTools/context menus are enabled only for `WB_EDITOR_DEVSERVER`; top-level navigation
  is restricted to `editor.local` (or the configured dev origin), and popups are blocked.

---

## 5. ✅ DONE (Phases 1–12 + 13a + migrations M1–M3a; 0/0 build, 50 C# + 26 editor tests)

- **Phase 1 — Scaffolding.** Solution, 5 src + 2 test projects, Project JSON model
  (ElementNode/Page/Project/Breakpoint), ProjectSerializer, and service interfaces.
- **Phase 2 — WPF shell.** AvalonDock dark shell: ribbon (File/Edit/Arrange/View/Help),
  6 dockable panels (Project Explorer, Layers, Components, Assets, Properties, Files) +
  canvas document, status bar, **command palette (Ctrl+Shift+P)**, keyboard shortcuts.
  Functional New/Open/Save/SaveAs (real `.wbproj` JSON). `UndoRedoService` implemented
  (stack ready; commands not yet pushed — see Phase 15).
- **Phase 3 — WebView2 integration.** Editor runs inside the canvas; full bidirectional
  bridge handshake (editor.ready / host.getInfo / editor.echo).
- **Phase 4 — React editor.** Infinite canvas (zoom-to-cursor, wheel/space/middle pan),
  numbered rulers, grid, element renderer from JSON; project model synced over the bridge;
  bidirectional zoom + breakpoint sync. Starter template seeded so the canvas has content.
- **Phase 5 — Drag-and-drop.** (5a) drag from palette → create elements + WPF Components
  click-insert, model sync back to host. (5b) move/reposition existing elements, reparent/
  nest into containers with drop-target highlight.
- **Phase 6 — Selection engine.** (6a) bounding box + 8 resize handles + selection→inspector.
  (6b) keyboard Delete/Ctrl+D/arrow-nudge/Esc + WPF Edit Delete/Duplicate. (6c) multi-select
  (shift-click) + marquee + group-move + multi delete/duplicate. (6d) alignment tools
  (align L/C/R/T/M/B + distribute) via in-editor toolbar + WPF Arrange ribbon. (6e) smart
  guides / snap lines (snap to elements' edges/centers + frame; magenta guides; unit-tested).
  (6f) rotate handles (`rotation` field on ElementNode).
- **Phase 7 — Property Inspector (fully editable, live two-way).** (7a) Layout (X/Y/W/H/
  Rotation/Opacity) + Enter-to-apply. (7b) Content (text) + Typography. (7c) Appearance
  (fill/border/radius/shadow/blend + color pickers). (7d) Advanced (overflow/z-index/
  visibility/cursor/filter/transition) + **editable Custom CSS** (add/remove rows, incl.
  CSS variables).
- **Phase 8 — Layers panel.** Two-way canvas selection, collapse/expand, hide/lock,
  inline rename, drag-reorder/reparent with drop adorners, and group/ungroup.
- **Phase 9 — Responsive engine.** Breakpoint style cascade, breakpoint-local editing,
  effective values in the inspector, and override indicators/tests.
- **Phase 10 — Project persistence.** Versioned schema migrations, folder-based project
  layout and Save As flow, file/asset panel refresh, debounced auto-save and recovery copy.
- **Phase 11 — HTML/CSS/JS exporter.** Semantic static output, shared deduplicated CSS,
  responsive media rules, safe attributes/text, export command/UI, and tests.
- **Phase 12 — React exporter.** Initial npm-ready React output with routing, shared CSS,
  export command/UI, and tests; upgraded to the current platform in Migration M2.
- **Migration M1 — Embedded editor to current Next.js stack (2026-08-13).** Replaced the
  Vite editor shell with Next.js 16.3.0 App Router static export, React/React DOM 19.2.8,
  TypeScript 7.0.2, production CSP, route error boundary, SSR-safe bridge/storage access,
  guarded copy into WPF `wwwroot`, origin-locked WebView2 navigation, and Next dev-server
  configuration. Explicit `experimental.useTypeScriptCli` makes Next invoke TS7's native
  project-local CLI instead of the removed legacy JavaScript compiler API. Registry audit: 0.
- **Migration M2 — exported React projects (2026-08-13).** Replaced the Vite/React 18 JSX
  scaffold with a pinned Next.js 16.3.0 App Router + React 19.2.8 + TypeScript 7.0.2 project.
  Exports now provide strict typed Server Components, nested routes, metadata/error/404 files,
  static export, `next/image`, normalized routes, filtered attributes/URLs, security headers,
  updated Export Next.js UI, and focused tests. A generated sample passed TS7 type-check,
  Next production build/static generation, and registry audit (0 vulnerabilities).
- **Phase 13a — asset model + safe import pipeline (2026-08-13).** Project schema v2 adds
  persisted asset metadata mirrored in TypeScript. `AssetService` owns project-local storage
  with an extension allowlist, per-category byte limits, content-signature checks, strict
  UTF-8/JSON validation, active/external SVG rejection, randomized names, SHA-256 metadata,
  atomic writes, path containment, and managed deletion. The Assets panel now uses this Core
  boundary and marks imports/deletions as project mutations for autosave/bridge persistence.
- **Migration M3a — repository safety baseline (2026-08-20).** Initialized Git on `main`,
  normalized repository text to LF through `.gitattributes` + `.editorconfig`, expanded ignore
  rules for generated build/typecheck/coverage output, local agent settings, environment files,
  and temporary backups, then captured the audited pre-hardening source as a baseline commit and
  annotated tag. No application behavior changed. A remote backup still needs a user-selected
  private Git host/repository before anything can be pushed externally.

### Standalone polish/fixes already done (user-requested)
- App renamed to **"Likha - Website Builder"** + logo as exe/window/taskbar icon.
- Canvas: removed device-frame shadow; added a **canvas background color picker** (persisted).
- Layers/Files tree text made readable (was black on dark).
- Dock layout persists across restarts; auto-hide airspace fixed.
- Property fields apply on **Enter**; dark dropdowns; working color picker.

---

## 6. ⛔ NOT DONE — stabilization M3b–M3f, then Phases 13–17

Deliver each split into sub-phases, one per turn.

- **Migration M3 — stabilization audit follow-up (M3a complete).** Before continuing feature
  work: M3b repairs authoritative host/editor state and the Phase 13a stale-asset overwrite;
  M3c makes saves atomic and adds dirty/recovery safeguards; M3d adds project validation and
  hardens HTML/CSS/export paths; M3e adds CI/lint/format/coverage/integration gates; M3f performs
  controlled dependency maintenance and migrates the WPF solution from .NET 8 to .NET 10 LTS.

- **Phase 13 — Asset manager (13a complete).** Remaining: 13b thumbnail/grid browser with
  category/search filtering and asset details; 13c drag image/SVG/video assets into the canvas
  and keep exported paths correct; 13d font/audio/document-specific workflows.
- **Phase 14 — Component library.** Prebuilt blocks (Navbar, Hero, Pricing, Testimonials,
  FAQ, Forms, Footer, 404, landing pages…) insertable onto the canvas.
- **Phase 15 — Undo/Redo integration.** `UndoRedoService` exists but NO mutations push
  commands yet. Wire editor mutations (and host edits) through the command/undo stack so
  Ctrl+Z/Y work. The Edit ribbon Undo/Redo are bound but currently no-op (empty stack).
  This likely needs an editor-side history (the model lives in the Zustand store) plus a
  bridge channel, or a host-side history of project snapshots.
- **Phase 16 — Performance.** Virtualized rendering, lazy loading, efficient diffing —
  target 10k+ elements, 60fps drag. (Current: full-project structuredClone per mutation +
  debounced full-project push. Fine for now, optimize here.)
- **Phase 17 — Final polish.** Theming, animations, context menus, tabs, accessibility,
  packaging/installer, production hardening. (Optionally rename the actual exe to Likha.exe
  here — currently AssemblyName=WebsiteBuilder is kept to avoid breaking data-folder/layout
  paths and tooling.)

### Known small TODOs / deferrals
- Copy/Paste ribbon commands are intentionally DISABLED (need a clipboard model — pick a phase).
- `transform` is not an inspector field (rotation covers the common case; advanced transform
  goes via Custom CSS). Revisit if needed.
- Group-resize (resizing a multi-selection bbox) not implemented — handles show for single
  selection only.
- SVG import intentionally rejects active content, animation, embedded/external references,
  and CSS URL references except local `url(#id)`. Raster/media/font parser vulnerabilities
  still depend on the patched OS/browser decoders; Phase 13 does not attempt file transcoding.

---

## 7. Memory note

The user's persistent memory already contains a detailed `website-builder-project.md` (all
the patterns above, per-phase) and `split-phases-preference.md` (the one-sub-phase-per-turn
rule). Today's date context in prior sessions was 2026-06; convert relative dates to absolute.

## 8. Suggested first action in the new session

Start **Migration M3b — authoritative state repair**. Fix the Phase 13a stale-asset overwrite
before adding the asset browser: host-originated mutations must reach the editor without echo
loops, whole-project updates need revision/conflict protection, and the bridge behavior needs
focused integration tests. Keep Phase 13b paused until M3b–M3f are complete.
