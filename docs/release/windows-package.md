# Windows release package

Phase 17c adds a reproducible, self-contained Windows x64 release package for Likha. Build it
from the repository root:

```powershell
.\scripts\package-release.ps1 -Version 0.1.0
```

The script performs a clean editor install and production export, publishes the .NET desktop app,
verifies that both `WebsiteBuilder.exe` and `wwwroot/index.html` exist, writes a per-file SHA-256
manifest, and creates `artifacts/release/Likha-<version>-<rid>.zip`.

The package is self-contained and ReadyToRun. Trimming and single-file publishing remain disabled:
WebView2, WPF, AvalonDock, dependency injection, and the embedded editor use resources or runtime
behavior for which an aggressively trimmed or bundled release would need a separate compatibility
campaign.

## Rehearsal result

The 2026-08-20 release rehearsal produced:

- Package: `Likha-0.1.0-win-x64.zip`
- ZIP bytes: `80,027,749`
- ZIP SHA-256: `72a18895139e4e936e567d78650c875709545a6e70180a8caa8491458995215d`
- Manifest payload: `554` files
- Debug symbols: none
- Startup smoke: the packaged executable remained healthy for the eight-second observation window
- Signature: unsigned, as expected without a publisher-owned certificate

The GitHub Actions tag/manual workflow creates the same unsigned portable artifact. It deliberately
uses read-only repository permissions and does not publish a release automatically.

## Authenticode signing boundary

When a publisher-owned code-signing certificate with a private key is available in
`Cert:\CurrentUser\My`, sign the executable during packaging:

```powershell
.\scripts\package-release.ps1 -Version 1.0.0 -CertificateThumbprint '<thumbprint>'
```

The script verifies the resulting signature and records the signed state in the manifest. A public
installer/MSIX also requires the publisher identity, certificate subject, desired distribution
channel, and upgrade identity. Those values must come from the project owner; they must not be
invented in source control.
