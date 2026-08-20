# Likha - Website Builder

A professional Windows desktop **visual website builder** (Webflow/Framer-class) built with
**C# / .NET 10 LTS / WPF / WebView2**, hosting a **Next.js + React + TypeScript** visual editor. Designs are stored as
a canonical **Project JSON** model and exported as clean **HTML5 / CSS3 / JavaScript** and **React**
source — never screenshots or canvas dumps.

> Status: **Phases 1–17 are complete.** The editor,
> secure asset pipeline, reusable block library, persistence, exporters, undo/redo, large-canvas
> performance architecture, and production package pipeline are implemented.

## Architecture

```
WebsiteBuilder.sln
├── src/
│   ├── WebsiteBuilder.App       WPF (.NET 10) shell + WebView2 host + DI/Host bootstrap
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
- **.NET 10 SDK** (the repository also supports its ignored local `.dotnet-sdk` runtime).
- **Node.js 24+** and **npm**.
- **WebView2 Runtime** (ships with current Windows 10/11; otherwise install the Evergreen runtime).

## Build & run

### Desktop app (WPF)
```sh
.\.dotnet-sdk\dotnet.exe build WebsiteBuilder.sln
.\.dotnet-sdk\dotnet.exe test WebsiteBuilder.sln
.\.dotnet-sdk\dotnet.exe run --project src/WebsiteBuilder.App
```
The app launches the complete dark-themed editor shell.

### React editor
```sh
cd src/WebsiteBuilder.Editor
npm ci
npm run dev      # http://127.0.0.1:3000
npm run build    # static export, then copies it to src/WebsiteBuilder.App/wwwroot
```
The WPF host loads this bundle inside WebView2 and establishes the typed JSON-RPC bridge handshake.

## Release package

Create a self-contained Windows x64 package (including the static editor) with:

```powershell
.\scripts\package-release.ps1 -Version 0.1.0
```

Output is written under `artifacts/release/` as a ZIP with a per-file SHA-256 manifest. Pass
`-CertificateThumbprint` to sign the executable with a private-key certificate in the current
user's Windows certificate store. The tag/manual release workflow produces the same unsigned CI
artifact; public distribution still requires a publisher-owned code-signing certificate and the
desired installer identity.
