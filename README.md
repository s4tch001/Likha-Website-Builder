# Likha - Website Builder

A professional Windows desktop **visual website builder** (Webflow/Framer-class) built with
**C# / .NET 8 / WPF / WebView2**, hosting a **Next.js + React + TypeScript** visual editor. Designs are stored as
a canonical **Project JSON** model and exported as clean **HTML5 / CSS3 / JavaScript** and **React**
source — never screenshots or canvas dumps.

> Status: **Phases 1–12, asset foundation 13a, and platform migrations M1–M2 complete.** The
> drag-and-drop editor, secure asset import, project persistence, and exporters are implemented.

## Architecture

```
WebsiteBuilder.sln
├── src/
│   ├── WebsiteBuilder.App       WPF (.NET 8) shell + WebView2 host + DI/Host bootstrap
│   ├── WebsiteBuilder.Core      Domain model (Project JSON), services, serialization — no UI deps
│   ├── WebsiteBuilder.Bridge    Typed JSON-RPC contract (IEditorBridge) between WPF and the editor
│   ├── WebsiteBuilder.CodeGen   Project JSON → HTML/CSS/JS and React emitters (pure, testable)
│   └── WebsiteBuilder.Editor    Next.js 16.3 + React 19.2 + TypeScript 7 + Zustand visual editor
└── tests/
    ├── WebsiteBuilder.Core.Tests
    └── WebsiteBuilder.CodeGen.Tests
```

**Data flow:** React Editor → Project JSON (single source of truth) → CodeGen → Export.

### Key technology decisions
- **Editor/exporter:** Next.js 16.3 App Router static export + React 19.2 + native TypeScript 7 + Zustand.
- **WPF ↔ Web bridge:** typed JSON-RPC over WebView2 `postMessage`.
- **WPF foundation:** CommunityToolkit.Mvvm + Microsoft.Extensions DI / Hosting.

## Prerequisites
- **.NET 8 SDK** — install with `winget install Microsoft.DotNet.SDK.8` or from <https://aka.ms/dotnet/download>.
- **Node.js 20.9+** and **npm** (verified with Node 26).
- **WebView2 Runtime** (ships with current Windows 10/11; otherwise install the Evergreen runtime).

## Build & run

### Desktop app (WPF)
```sh
dotnet build WebsiteBuilder.sln
dotnet test
dotnet run --project src/WebsiteBuilder.App
```
The app launches the complete dark-themed editor shell.

### React editor
```sh
cd src/WebsiteBuilder.Editor
npm install
npm run dev      # http://127.0.0.1:3000
npm run build    # static export, then copies it to src/WebsiteBuilder.App/wwwroot
```
The WPF host loads this bundle inside WebView2 and establishes the typed JSON-RPC bridge handshake.

## Roadmap
Phase 13 continues with the asset browser and canvas integration; Phases 14–17 cover the component
library, undo/redo, performance work, and final polish.
