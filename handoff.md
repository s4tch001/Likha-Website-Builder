# Likha - Website Builder — Project Handoff

> Handoff document for continuing this project in a new session. Read this fully
> before starting. It captures the architecture, conventions, what is DONE, what
> remains, and the practical gotchas learned along the way.

---

## 0. What this is

**Likha - Website Builder** — a professional Windows desktop **visual website
builder** (Webflow/Framer-class). Locked stack:

- **Host:** C# / .NET 10 LTS / **WPF** (Windows-only), hosting the editor in **WebView2**.
- **Editor:** **Next.js 16.3.1 App Router + React 19.2.8 + TypeScript 7.0.2 + Zustand 4.5.7**,
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
│   ├── WebsiteBuilder.App        WPF shell (net10.0-windows). AssemblyName=WebsiteBuilder (exe is WebsiteBuilder.exe — INTERNAL name kept; display name is "Likha - Website Builder")
│   ├── WebsiteBuilder.Core       Domain model (Project/Page/ElementNode/Breakpoint), services, ProjectSerializer. No UI deps.
│   ├── WebsiteBuilder.Bridge     JSON-RPC contract: IEditorBridge, BridgeMessage{id,type,method,payload,error}
│   ├── WebsiteBuilder.CodeGen    ICodeGenerator + working static HTML and React emitters
│   └── WebsiteBuilder.Editor     Next.js/React/TS/Zustand editor. Static export → ../WebsiteBuilder.App/wwwroot
└── tests/
    ├── WebsiteBuilder.Core.Tests      (xUnit) 42 tests
    └── WebsiteBuilder.CodeGen.Tests   (xUnit) 27 tests
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

`global.json` requires .NET 10 (`10.0.100`, `latestFeature`). This workspace has the
current SDK 10.0.400 in a project-local, ignored directory; use:
```
.\.dotnet-sdk\dotnet.exe
```
The machine-wide SDK is still .NET 8, so plain `dotnet` will not satisfy `global.json`
until .NET 10 is installed system-wide. Node 24 + npm and WebView2 Runtime 146 are present.

### Build & test
```sh
# Editor (from src/WebsiteBuilder.Editor):
npm ci                 # exact, audited lockfile install
npm run build          # next build (static export) → copies out/ to ../WebsiteBuilder.App/wwwroot
npm run format:check   # Prettier gate
npm run lint           # ESLint + Next/React rules
npm run typecheck      # TS 7 native `tsc --noEmit`
npm run test:coverage  # 36 tests + enforced coverage floor

# C# (from repo root):
.\.dotnet-sdk\dotnet.exe restore WebsiteBuilder.sln
.\.dotnet-sdk\dotnet.exe build WebsiteBuilder.sln -c Release --no-restore
.\.dotnet-sdk\dotnet.exe test WebsiteBuilder.sln -c Release --no-restore  # 69 tests
.\.dotnet-sdk\dotnet.exe run --project src/WebsiteBuilder.App
```

### 🔴 CRITICAL GOTCHA: after editing the React editor, you MUST `dotnet build` the App
`npm run build` creates a Next.js static export in `out/`, then the guarded copy script
replaces the **source** `wwwroot/`. The running exe loads its **own
copy** in `bin/Debug/net10.0-windows/wwwroot/`, copied by MSBuild (Content,
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
- The host owns the authoritative monotonic revision. `host.getProject` and `project.load`
  carry `{project, revision}`. Editor mutations are serialized and sent through
  `host.applyProjectUpdate {baseRevision, project}`; `TryApplyEditorUpdate` accepts only
  the exact current revision. On conflict the host snapshot replaces the stale editor copy.
- Host-originated mutations use `ApplyHostUpdate`, increment the revision, raise `Mutated`
  for persistence/panels and `HostMutated` for `project.load`. Editor-originated accepted
  snapshots deliberately do not raise `HostMutated`, preventing echo loops. Do not restore
  the removed legacy full-project event path because it bypasses conflict protection.
- Selection: editor publishes `editor.selectionChanged {ids, element}` →
  `EditorSession.SelectionChanged` → Property Inspector loads it.
- Host→editor edits (e.g. Property Inspector): `editor.setStyle/setGeometry/setRotation/
  setText/insertElement/deleteSelected/duplicateSelected/align/setZoom/setBreakpoint`.
- Host handler string-returns are PARSED to JS objects by the C# bridge (so
  `host.getProject` resolves to a project OBJECT on the TS side, not a string).
- Both bridge directions enforce a 15-second timeout and pending-request cleanup. The C#
  transport also enforces the trusted WebView origin and a 16 MiB message ceiling.

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

## 5. ✅ DONE (Phases 1–13 + 14a + migrations M1–M3f; 0/0 build, 75 C# + 41 editor tests)

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
- **Phase 13b — asset browser (2026-08-20).** Replaced the list-only Assets panel with a
  searchable, category-filtered thumbnail grid, bounded raster thumbnail decoding, selection
  details (kind/media type/size/import time/path), and contextual Insert/Apply Font/Delete
  actions. Missing managed files remain unavailable instead of being opened through raw paths.
- **Phase 13c — canvas drag/drop and export assets (2026-08-20).** Assets can be dragged from
  WPF into the WebView2 canvas (with a metadata-id contract plus managed-filename fallback) or
  inserted from the details action. The editor accepts only metadata that exactly matches the
  active project's canonical asset list and resolves previews through a project-scoped virtual
  origin. Image/SVG/icon and video nodes render semantically. Static exports copy assets under
  `Assets/`, rebase nested-page URLs, and Next.js exports copy them under `public/Assets/` with
  root-relative URLs. Export validates its complete contained output plan before writing and
  streams binary files through atomic same-directory temporary files.
- **Phase 13d — font/audio/document workflows (2026-08-20).** Audio assets insert semantic
  controlled players; documents insert downloadable links; managed fonts expose deterministic
  family names, live `@font-face` preview declarations, and an Apply Font action for the canvas
  selection. Both exporters emit target-correct font URLs. Referenced assets cannot be deleted,
  and project validation rejects unknown kinds, malformed metadata, and dangling managed URLs.
  CSP allows only the dedicated managed-asset origin for editor images/media/fonts. Focused
  Core/editor/codegen tests cover canonical insertion, spoof rejection, font URLs, nested export
  paths, usage tracking, and atomic binary replacement.
- **Phase 14a — component definition/catalog foundation (2026-08-20).** Added a compiled,
  first-party Core component contract and validator, a searchable Blocks category in the WPF
  Components panel, and dedicated `editor.insertComponent` bridge/store handling. Component
  trees are validated on both sides of the bridge, cannot carry implicit managed-asset paths,
  are deep-cloned with fresh IDs, positioned as one subtree, selected, and synchronized as one
  revision. `Simple Hero` proves the end-to-end path. ADR 0001 records why inserted blocks become
  ordinary elements instead of persisted live template references. Focused tests cover catalog
  validation, unsafe definitions, fresh IDs, atomic insertion, and spoof rejection.
- **Migration M3a — repository safety baseline (2026-08-20).** Initialized Git on `main`,
  normalized repository text to LF through `.gitattributes` + `.editorconfig`, expanded ignore
  rules for generated build/typecheck/coverage output, local agent settings, environment files,
  and temporary backups, then captured the audited pre-hardening source as a baseline commit and
  annotated tag. No application behavior changed. A remote backup still needs a user-selected
  private Git host/repository before anything can be pushed externally.
- **Migration M3b — authoritative state synchronization (2026-08-20).** Added host-owned
  monotonic revisions, revision-checked editor updates, conflict recovery, serialized editor
  pushes, and host→editor mutation publication. Asset imports/deletions can no longer be
  overwritten by a stale editor snapshot. Hardened bridge origins, size/time limits, pending
  cleanup, and failure handling; added focused revision and bridge integration tests.
- **Migration M3c — durable persistence (2026-08-20).** Project/recovery writes now use
  same-directory temporary files, disk flush, atomic replacement, and rolling backups. Saves
  are serialized and only clear dirty state for the saved revision. New/Open/Close now have
  Save/Discard/Cancel guards, startup recovery restore/discard handling, and Save As refuses
  to overwrite another existing project folder silently.
- **Migration M3d — validation/export containment (2026-08-20).** Added bounded runtime
  project validation in C# and TypeScript, unique IDs/routes, finite geometry, depth/count/
  size limits, safe attribute/URL/CSS policies, and validation on deserialize, bridge updates,
  serialize, and code generation. Export paths are fully resolved and root-contained, writes
  are atomic, nested-route asset references are correct, and responsive root CSS is page-scoped.
- **Migration M3e — continuous quality gates (2026-08-20).** Added pinned ESLint 9 + Next
  rules (ESLint 10 is not yet compatible with the transitive Next plugin), Prettier, TypeScript
  native typecheck, Vitest/Coverlet coverage floors, bridge/host integration tests, Windows CI,
  Dependabot, npm/NuGet vulnerability audits, and formatting gates. Coverage baseline: editor
  65.24% statements/56.19% branches/73.4% functions/65.31% lines; Core 78.71% lines; CodeGen
  85.31% lines.
- **Migration M3f — .NET 10 and controlled maintenance (2026-08-20).** Retargeted all C#
  projects to .NET 10 LTS and verified with SDK 10.0.400. Updated CommunityToolkit.Mvvm 8.4.2,
  Microsoft.Extensions.Hosting 10.0.11, WebView2 1.0.4129.50, test SDK 18.9.0, xUnit 2.9.3,
  runner 3.1.5, Next.js/editor and generated exports 16.3.1, and Vitest 4.1.11. The editor and
  generated projects use `@typescript/native` 7.0.2 for the Go compiler plus the official
  `typescript`→`@typescript/typescript6` 6.0.3 compatibility alias for compiler-API consumers;
  `experimental.useTypeScriptCli` keeps Next on native TS7. Clean install/build/tests/audits pass.

### Standalone polish/fixes already done (user-requested)
- App renamed to **"Likha - Website Builder"** + logo as exe/window/taskbar icon.
- Canvas: removed device-frame shadow; added a **canvas background color picker** (persisted).
- Layers/Files tree text made readable (was black on dark).
- Dock layout persists across restarts; auto-hide airspace fixed.
- Property fields apply on **Enter**; dark dropdowns; working color picker.

---

## 6. ⛔ NOT DONE — Phase 14 onward

- **Phase 14 — Component library (14a complete).** Remaining planned splits: 14b navigation,
  hero, footer, and 404 blocks; 14c pricing, testimonials, FAQ, and form blocks; 14d complete
  landing-page assemblies plus component-browser drag/drop and visual preview polish.
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
- Controlled major-version deferrals: AvalonDock 5 needs a dedicated docking/layout migration;
  Zustand 5 belongs with the Phase 16 state/performance work; ESLint 10 remains blocked by
  `eslint-plugin-import`'s ESLint 9 peer range under `eslint-config-next`; xUnit VS runner 4 and
  its analyzer-major change should be handled as a separate test-infrastructure migration.
- The Git repository has no remote. Configure a user-selected private remote before pushing;
  do not guess a host or repository URL.

---

## 7. Memory note

The user's persistent memory already contains a detailed `website-builder-project.md` (all
the patterns above, per-phase) and `split-phases-preference.md` (the one-sub-phase-per-turn
rule). Today's date context in prior sessions was 2026-06; convert relative dates to absolute.

## 8. Suggested first action in the new session

Continue with **Phase 14b — navigation, hero, footer, and 404 blocks**. The user explicitly
asked Codex to continue through later phases/sub-phases and to update this handoff after every
completed sub-phase, stopping work when remaining Codex usage reaches 10%. Preserve the Phase
13 canonical-asset boundary plus the M3 revision, validation, persistence, coverage, and
dependency gates.
