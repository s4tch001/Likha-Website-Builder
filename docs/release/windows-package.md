# Windows release package

The release pipeline creates both a reproducible portable package and a per-user Windows x64
installer. Build them from the repository root:

```powershell
.\scripts\package-release.ps1 -Version 0.2.0
```

The script performs a clean editor install and production export, publishes the .NET desktop app,
verifies that both `WebsiteBuilder.exe` and `wwwroot/index.html` exist, writes a per-file SHA-256
manifest, and creates:

- `artifacts/release/Likha-<version>-win-x64.zip`
- `artifacts/release/Likha-<version>-win-x64-setup.exe`
- `artifacts/release/Likha-<version>-win-x64-setup.exe.sha256`

The setup compiler is pinned to the official signed Inno Setup 7.1.0 x64 release and verified
against its publisher-provided SHA-256 before use:

```powershell
.\scripts\install-inno.ps1
```

The setup installs without elevation under the current user's local Programs directory, registers
an uninstaller, and creates Start Menu plus default-on Desktop shortcuts. It never removes project
folders or the user's local layout data.

The package is self-contained and ReadyToRun. Trimming and single-file publishing remain disabled:
WebView2, WPF, AvalonDock, dependency injection, and the embedded editor use resources or runtime
behavior for which an aggressively trimmed or bundled release would need a separate compatibility
campaign.

## Rehearsal result

The 2026-08-20 installer rehearsal produced:

- Installer: `Likha-0.2.0-win-x64-setup.exe`
- Installer bytes: `55,604,928`
- Installer SHA-256: `4d7bde1f3428be580b6c446d0b0a1691d2b2f06b32d18f5a177b3ef3cbab7cd5`
- Install/uninstall: both returned exit code 0 and left no test payload or uninstall registration
- Startup smoke: the installed executable remained healthy for the eight-second observation window
- Signature: unsigned, as expected for the owner's personal build

The GitHub Actions tag/manual workflow creates the same unsigned ZIP, installer, and checksum as a
retained workflow artifact. It deliberately uses read-only repository permissions and does not
publish a GitHub Release automatically.

## Authenticode signing boundary

When a publisher-owned code-signing certificate with a private key is available in
`Cert:\CurrentUser\My`, sign the executable during packaging:

```powershell
.\scripts\package-release.ps1 -Version 1.0.0 -CertificateThumbprint '<thumbprint>'
```

The script verifies both the application and final installer signatures and records the application
signed state in the manifest. Public distribution still requires a publisher-owned certificate;
the unsigned setup is intended only for the owner's personal use.
