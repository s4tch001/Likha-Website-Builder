using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using WebsiteBuilder.App.Bridge;
using WebsiteBuilder.Bridge;
using WebsiteBuilder.Core.Components;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Serialization;
using WebsiteBuilder.Core.Services;
using WebsiteBuilder.Core.Validation;

namespace WebsiteBuilder.App.Services;

/// <summary>
/// Owns the WebView2-hosted editor for the application: creates the runtime
/// environment, serves the React bundle, establishes the <see cref="IEditorBridge"/>
/// and performs the startup handshake (editor.ready → host.getInfo → editor.echo).
/// Registered as a singleton so any phase can reach the live bridge via
/// <see cref="Bridge"/>.
/// </summary>
public sealed class EditorSession
{
    /// <summary>Virtual host the local editor bundle is served from.</summary>
    public const string VirtualHost = "editor.local";

    /// <summary>Read-only virtual origin mapped only to the active project's managed Assets folder.</summary>
    public const string AssetVirtualHost = "project-assets.local";

    private readonly bool _useDevServer;
    private readonly string _devServerUrl;
    private readonly IProjectService _projects;

    private WebView2EditorBridge? _bridge;
    private CoreWebView2? _core;
    private bool _attached;

    public EditorSession(IConfiguration configuration, IProjectService projects)
    {
        _projects = projects;
        _projects.CurrentChanged += (_, _) => RefreshAssetMapping();
        _projects.HostMutated += (_, _) => RefreshAssetMapping();

        _useDevServer =
            Environment.GetEnvironmentVariable("WB_EDITOR_DEVSERVER") == "1"
            || configuration.GetValue("Editor:UseDevServer", false);

        _devServerUrl =
            Environment.GetEnvironmentVariable("WB_EDITOR_DEVURL")
            ?? configuration.GetValue("Editor:DevServerUrl", "http://127.0.0.1:3000")
            ?? "http://127.0.0.1:3000";

        // Push replacements and host-originated mutations into the editor. Editor
        // mutations deliberately do not raise HostMutated, preventing echo loops.
        _projects.CurrentChanged += (_, _) => PublishProject();
        _projects.HostMutated += (_, _) => PublishProject();
    }

    /// <summary>Raised when the editor reports a viewport zoom change (percent).</summary>
    public event EventHandler<double>? ZoomChanged;

    /// <summary>Raised when the canvas selection changes (null = nothing selected).</summary>
    public event EventHandler<ElementNode?>? SelectionChanged;

    /// <summary>Id of the primary selected element, or null.</summary>
    public string? SelectedId { get; private set; }

    /// <summary>Id of the breakpoint the selection was reported for (null = base/none).</summary>
    public string? SelectedBreakpointId { get; private set; }

    /// <summary>Human-readable label of the active breakpoint (e.g. "Mobile").</summary>
    public string? SelectedBreakpointLabel { get; private set; }

    /// <summary>True when the active breakpoint is the base (edits apply to base styles).</summary>
    public bool IsBaseBreakpoint { get; private set; } = true;

    /// <summary>CSS keys overridden at the active breakpoint for the current selection.</summary>
    public IReadOnlyList<string> OverriddenKeys { get; private set; } = Array.Empty<string>();

    /// <summary>Number of elements currently selected on the canvas.</summary>
    public int SelectedCount { get; private set; }

    /// <summary>Whether anything is currently selected on the canvas.</summary>
    public bool HasSelection => SelectedCount > 0;

    public bool CanUndo { get; private set; }

    public bool CanRedo { get; private set; }

    public event EventHandler? HistoryChanged;

    public bool CanPaste { get; private set; }

    public event EventHandler? ClipboardChanged;

    /// <summary>The live bridge to the editor, or null until <see cref="AttachAsync"/> completes.</summary>
    public IEditorBridge? Bridge => _bridge;

    /// <summary>True once the editor has sent its <c>editor.ready</c> event.</summary>
    public bool IsReady => _bridge?.IsReady ?? false;

    /// <summary>Latest human-readable connection status (shown on the canvas).</summary>
    public string Status { get; private set; } = "Idle";

    /// <summary>Raised whenever <see cref="Status"/> changes (always on the UI thread).</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Binds the session to a WebView2 control: initializes the runtime, wires the
    /// bridge and navigates to the editor. Safe to call once per control.
    /// </summary>
    public async Task AttachAsync(WebView2 webView)
    {
        if (_attached)
        {
            return;
        }

        _attached = true;

        try
        {
            SetStatus("Initializing WebView2 runtime…");

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebsiteBuilder", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment
                .CreateAsync(userDataFolder: userDataFolder)
                .ConfigureAwait(true);
            await webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

            var core = webView.CoreWebView2;
            _core = core;
            core.Settings.AreDevToolsEnabled = _useDevServer;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = _useDevServer;
            core.NavigationStarting += (_, args) =>
            {
                if (!IsAllowedEditorUri(args.Uri))
                {
                    args.Cancel = true;
                    SetStatus("Blocked navigation outside the editor origin.");
                }
            };
            core.NewWindowRequested += (_, args) => args.Handled = true;

            string url;
            if (_useDevServer)
            {
                url = _devServerUrl;
            }
            else
            {
                var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                if (!File.Exists(Path.Combine(wwwroot, "index.html")))
                {
                    SetStatus($"Editor bundle not found at '{wwwroot}'. Run 'npm run build' in src/WebsiteBuilder.Editor.");
                    return;
                }

                core.SetVirtualHostNameToFolderMapping(
                    VirtualHost, wwwroot, CoreWebView2HostResourceAccessKind.Allow);
                url = $"https://{VirtualHost}/index.html";
            }


            RefreshAssetMapping();

            var allowedOrigin = new Uri(url).GetLeftPart(UriPartial.Authority);
            _bridge = new WebView2EditorBridge(
                core,
                webView.Dispatcher,
                ProjectSerializer.Options,
                allowedOrigin);
            _bridge.EventReceived += OnEditorEvent;
            RegisterHostHandlers(_bridge);

            SetStatus($"Loading editor: {url}");
            core.Navigate(url);
        }
        catch (Exception ex)
        {
            SetStatus($"WebView2 initialization failed: {ex.Message}");
        }
    }

    private bool IsAllowedEditorUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!_useDevServer)
        {
            return uri.Scheme == Uri.UriSchemeHttps
                && string.Equals(uri.Host, VirtualHost, StringComparison.OrdinalIgnoreCase);
        }

        return Uri.TryCreate(_devServerUrl, UriKind.Absolute, out var devUri)
            && string.Equals(uri.Scheme, devUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, devUri.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == devUri.Port;
    }

    private void RegisterHostHandlers(WebView2EditorBridge bridge)
    {
        // editor → host request: returns metadata about the host application.
        bridge.RegisterHandler("host.getInfo", (_, _) =>
        {
            var info = new HostInfo("Likha", "0.1.0", "WPF / WebView2");
            return Task.FromResult<string?>(JsonSerializer.Serialize(info, ProjectSerializer.Options));
        });

        // editor → host request: returns the authoritative project plus its
        // in-memory revision for optimistic concurrency checks.
        bridge.RegisterHandler("host.getProject", (_, _) =>
        {
            var snapshot = _projects.Current is null
                ? null
                : new ProjectSyncEnvelope(_projects.Current, _projects.Revision);
            var json = snapshot is null
                ? null
                : JsonSerializer.Serialize(snapshot, ProjectSerializer.Options);
            return Task.FromResult(json);
        });

        // Editor snapshots are accepted only when based on the current host
        // revision. A conflict returns the authoritative model for resync.
        bridge.RegisterHandler("host.applyProjectUpdate", (payloadJson, _) =>
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new InvalidDataException("Project update payload is missing.");
            }

            var request = JsonSerializer.Deserialize<ProjectUpdateRequest>(payloadJson, ProjectSerializer.Options)
                ?? throw new InvalidDataException("Project update payload is invalid.");
            ProjectValidator.ValidateAndThrow(request.Project);
            var accepted = _projects.TryApplyEditorUpdate(
                request.Project,
                request.BaseRevision,
                out var revision);

            var response = new ProjectUpdateResponse(
                accepted,
                revision,
                accepted ? null : _projects.Current);
            return Task.FromResult<string?>(JsonSerializer.Serialize(response, ProjectSerializer.Options));
        });
    }

    private async void OnEditorEvent(object? sender, BridgeEventArgs e)
    {
        switch (e.Method)
        {
            case "editor.ready":
                await OnEditorReadyAsync().ConfigureAwait(true);
                break;

            case "editor.viewChanged":
                OnViewChanged(e.PayloadJson);
                break;

            case "editor.rendered":
                OnEditorRendered(e.PayloadJson);
                break;

            case "editor.selftestResult":
                if (!string.IsNullOrEmpty(e.PayloadJson))
                {
                    using (var doc = JsonDocument.Parse(e.PayloadJson))
                    {
                        var parent = doc.RootElement.TryGetProperty("parentId", out var p) ? p.GetString() : "?";
                        SetStatus($"Self-test: hero-button reparented under '{parent}'");
                    }
                }

                break;

            case "editor.selectionChanged":
                OnSelectionChanged(e.PayloadJson);
                break;

            case "editor.historyChanged":
                OnHistoryChanged(e.PayloadJson);
                break;

            case "editor.clipboardChanged":
                OnClipboardChanged(e.PayloadJson);
                break;

            case "editor.rotateResult":
                if (!string.IsNullOrEmpty(e.PayloadJson))
                {
                    using (var doc = JsonDocument.Parse(e.PayloadJson))
                    {
                        var rot = doc.RootElement.TryGetProperty("rotation", out var r) ? r.ToString() : "?";
                        SetStatus($"Rotate · feature-card rotation = {rot}°");
                    }
                }

                break;

            case "editor.alignResult":
                if (!string.IsNullOrEmpty(e.PayloadJson))
                {
                    using (var doc = JsonDocument.Parse(e.PayloadJson))
                    {
                        var a = doc.RootElement.TryGetProperty("headingRight", out var hr) ? hr.ToString() : "?";
                        var b = doc.RootElement.TryGetProperty("subtitleRight", out var sr) ? sr.ToString() : "?";
                        SetStatus($"Align right · right edges: {a} and {b}");
                    }
                }

                break;
        }
    }

    private void OnSelectionChanged(string? payloadJson)
    {
        ElementNode? node = null;
        var count = 0;

        if (!string.IsNullOrEmpty(payloadJson) && payloadJson != "null")
        {
            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Array)
                {
                    count = ids.GetArrayLength();
                }

                if (root.TryGetProperty("element", out var element) && element.ValueKind == JsonValueKind.Object)
                {
                    node = element.Deserialize<ElementNode>(ProjectSerializer.Options);
                }

                // The editor resolves the cascade for the active breakpoint and sends
                // the effective styles; the inspector shows/edit those values so it
                // reflects whichever breakpoint is being authored.
                if (node is not null
                    && root.TryGetProperty("effective", out var eff)
                    && eff.ValueKind == JsonValueKind.Object)
                {
                    var effective = eff.Deserialize<Dictionary<string, string>>(ProjectSerializer.Options);
                    if (effective is not null)
                    {
                        node.Styles = effective;
                    }
                }

                SelectedBreakpointId = root.TryGetProperty("breakpointId", out var bp) ? bp.GetString() : null;
                SelectedBreakpointLabel = root.TryGetProperty("breakpointLabel", out var bl) ? bl.GetString() : null;
                IsBaseBreakpoint = !root.TryGetProperty("isBaseBreakpoint", out var ib) || ib.GetBoolean();
                OverriddenKeys = root.TryGetProperty("overridden", out var ov) && ov.ValueKind == JsonValueKind.Array
                    ? ov.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => s.Length > 0).ToArray()
                    : Array.Empty<string>();
            }
            catch (JsonException)
            {
                node = null;
                count = 0;
            }
        }

        if (node is null)
        {
            // No single selection → clear breakpoint-override context.
            IsBaseBreakpoint = true;
            SelectedBreakpointLabel = null;
            OverriddenKeys = Array.Empty<string>();
        }

        SelectedId = node?.Id;
        SelectedCount = count;
        SelectionChanged?.Invoke(this, node);
        SetStatus(count switch
        {
            0 => "Selection cleared",
            1 => $"Selected {node?.Type} · {node?.Id}",
            _ => $"{count} elements selected",
        });
    }

    /// <summary>Selects a single element on the canvas (null clears the selection).</summary>
    public void SelectElement(string? id) => _ = _bridge?.PublishAsync("editor.select", new { id });

    /// <summary>Sets an element's editor-only hidden flag.</summary>
    public void SetHidden(string id, bool value) => _ = _bridge?.PublishAsync("editor.setHidden", new { id, value });

    /// <summary>Sets an element's editor-only locked flag.</summary>
    public void SetLocked(string id, bool value) => _ = _bridge?.PublishAsync("editor.setLocked", new { id, value });

    /// <summary>Sets an element's author-facing name (empty clears it).</summary>
    public void Rename(string id, string name) => _ = _bridge?.PublishAsync("editor.rename", new { id, name });

    /// <summary>Moves an element under a new parent at a specific child index (Layers reorder).</summary>
    public void ReorderElement(string id, string parentId, int index)
        => _ = _bridge?.PublishAsync("editor.reorder", new { id, parentId, index });

    /// <summary>Groups the current selection into a new container.</summary>
    public void GroupSelection() => _ = _bridge?.PublishAsync("editor.group", new { });

    /// <summary>Ungroups the given container (or the current selection if null).</summary>
    public void Ungroup(string? id = null) => _ = _bridge?.PublishAsync("editor.ungroup", new { id });

    /// <summary>Asks the editor to delete the currently selected element.</summary>
    public void DeleteSelected() => _ = _bridge?.PublishAsync("editor.deleteSelected", new { });

    /// <summary>Asks the editor to duplicate the currently selected element.</summary>
    public void DuplicateSelected() => _ = _bridge?.PublishAsync("editor.duplicateSelected", new { });

    public void Undo() => _ = _bridge?.PublishAsync("editor.undo", new { });

    public void Redo() => _ = _bridge?.PublishAsync("editor.redo", new { });

    public void Copy() => _ = _bridge?.PublishAsync("editor.copy", new { });

    public void Cut() => _ = _bridge?.PublishAsync("editor.cut", new { });

    public void Paste() => _ = _bridge?.PublishAsync("editor.paste", new { });

    /// <summary>Aligns/distributes the current selection (mode = left/hcenter/right/top/vmiddle/bottom/distH/distV).</summary>
    public void Align(string mode) => _ = _bridge?.PublishAsync("editor.align", new { mode });

    /// <summary>Sets or clears a single CSS style on an element (empty value clears it).</summary>
    public void SetStyle(string id, string name, string value)
        => _ = _bridge?.PublishAsync("editor.setStyle", new { id, name, value });

    /// <summary>Updates any provided geometry fields of an element (nulls are left unchanged).</summary>
    public void SetGeometry(string id, double? x = null, double? y = null, double? width = null, double? height = null)
        => _ = _bridge?.PublishAsync("editor.setGeometry", new { id, x, y, width, height });

    /// <summary>Sets an element's rotation in degrees.</summary>
    public void SetRotation(string id, double deg)
        => _ = _bridge?.PublishAsync("editor.setRotation", new { id, deg });

    /// <summary>Sets an element's inline text content.</summary>
    public void SetText(string id, string text)
        => _ = _bridge?.PublishAsync("editor.setText", new { id, text });

    private void OnEditorRendered(string? payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            var name = root.TryGetProperty("project", out var p) ? p.GetString() : "?";
            var elements = root.TryGetProperty("elements", out var e) ? e.GetInt32() : 0;
            SetStatus($"Editor rendered project '{name}' · {elements} elements");
        }
        catch (JsonException)
        {
            // Diagnostics only.
        }
    }

    private void OnHistoryChanged(string? payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            CanUndo = root.TryGetProperty("canUndo", out var undo) && undo.GetBoolean();
            CanRedo = root.TryGetProperty("canRedo", out var redo) && redo.GetBoolean();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (JsonException)
        {
            // Ignore malformed diagnostics/state events from the embedded editor.
        }
    }

    private void OnClipboardChanged(string? payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            CanPaste = doc.RootElement.TryGetProperty("canPaste", out var paste) && paste.GetBoolean();
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (JsonException)
        {
            // Ignore malformed editor state events.
        }
    }

    private async Task OnEditorReadyAsync()
    {
        if (_bridge is null)
        {
            return;
        }

        _bridge.IsReady = true;
        SetStatus("Connected — editor.ready received. Verifying round-trip…");

        try
        {
            var response = await _bridge
                .InvokeAsync<EchoRequest, EchoResponse>("editor.echo", new EchoRequest("hello from host"))
                .ConfigureAwait(true);

            SetStatus($"Bridge ready ✓  editor replied: \"{response.Reply}\"");

            // Headless verification hook: exercise insert → editor mutate → push-back,
            // then a move + reparent of an existing element.
            if (Environment.GetEnvironmentVariable("WB_SELFTEST") == "1")
            {
                InsertElement("Button");
                InsertElement("Card");
                _ = _bridge.PublishAsync("editor.runSelfTest", new { });
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Connected, but echo round-trip failed: {ex.Message}");
        }
    }

    private void OnViewChanged(string? payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("zoom", out var zoom)
                && zoom.TryGetDouble(out var value))
            {
                ZoomChanged?.Invoke(this, value);
            }
        }
        catch (JsonException)
        {
            // Ignore malformed view updates.
        }
    }

    /// <summary>Pushes the current project to the editor (no-op if not yet connected).</summary>
    public void PublishProject()
    {
        if (_bridge is null || _projects.Current is null)
        {
            return;
        }

        var snapshot = new ProjectSyncEnvelope(_projects.Current, _projects.Revision);
        var element = JsonSerializer.SerializeToElement(snapshot, ProjectSerializer.Options);
        _ = _bridge.PublishAsync("project.load", element);
    }

    /// <summary>Asks the editor to set its zoom level (percent).</summary>
    public void SetZoom(double zoomPercent)
        => _ = _bridge?.PublishAsync("editor.setZoom", new { zoom = zoomPercent });

    /// <summary>Asks the editor to switch the active responsive breakpoint.</summary>
    public void SetBreakpoint(string breakpointId)
        => _ = _bridge?.PublishAsync("editor.setBreakpoint", new { id = breakpointId });

    /// <summary>Asks the editor to insert a new element of the given type onto the canvas.</summary>
    public void InsertElement(string elementType)
        => _ = _bridge?.PublishAsync("editor.insertElement", new { type = elementType });

    /// <summary>Requests insertion using only canonical asset metadata from the current project.</summary>
    public void InsertAsset(ProjectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (_projects.Current?.Assets.Any(candidate =>
                string.Equals(candidate.Id, asset.Id, StringComparison.Ordinal)
                && string.Equals(candidate.RelativePath, asset.RelativePath, StringComparison.Ordinal)) != true)
        {
            SetStatus("Asset insertion rejected: metadata is not part of the current project.");
            return;
        }

        _ = _bridge?.PublishAsync("editor.insertAsset", new { asset });
    }

    /// <summary>Inserts a validated first-party component tree through the host bridge.</summary>
    public void InsertComponent(ComponentDefinition definition)
    {
        ComponentDefinitionValidator.ValidateAndThrow(definition);
        _ = _bridge?.PublishAsync("editor.insertComponent", new
        {
            componentId = definition.Id,
            root = definition.Root,
        });
    }

    private void RefreshAssetMapping()
    {
        if (_core is null)
        {
            return;
        }

        try
        {
            _core.ClearVirtualHostNameToFolderMapping(AssetVirtualHost);
            if (_projects.ProjectDirectory is not { } directory)
            {
                return;
            }

            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            var assets = Path.GetFullPath(Path.Combine(root, AssetService.AssetsFolderName));
            var prefix = root + Path.DirectorySeparatorChar;
            if (!assets.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(assets)
                || (File.GetAttributes(assets) & FileAttributes.ReparsePoint) != 0)
            {
                return;
            }

            _core.SetVirtualHostNameToFolderMapping(
                AssetVirtualHost,
                assets,
                CoreWebView2HostResourceAccessKind.Allow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SetStatus("Managed asset preview mapping is unavailable.");
        }
    }

    private void SetStatus(string status)
    {
        Status = status;

        var logPath = Environment.GetEnvironmentVariable("WB_TRACE_EDITOR");
        if (!string.IsNullOrEmpty(logPath))
        {
            try
            {
                File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {status}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Diagnostics only; never fail the app over a log write.
            }
        }

        StatusChanged?.Invoke(this, status);
    }
}

internal sealed record ProjectSyncEnvelope(Project Project, long Revision);

internal sealed record ProjectUpdateRequest(long BaseRevision, Project Project);

internal sealed record ProjectUpdateResponse(bool Accepted, long Revision, Project? Project);
