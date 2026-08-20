namespace WebsiteBuilder.Bridge;

/// <summary>
/// Strongly-typed, transport-agnostic channel to the React editor running in
/// WebView2. Phase 3 supplies a <c>WebView2EditorBridge</c> implementation over
/// <c>CoreWebView2.WebMessageReceived</c> / <c>PostWebMessageAsJson</c>; tests can
/// supply an in-memory fake. Every later phase talks to the editor only through
/// this interface.
/// </summary>
public interface IEditorBridge
{
    /// <summary>True once the editor has loaded and completed its handshake.</summary>
    bool IsReady { get; }

    /// <summary>Raised when the editor sends an <see cref="BridgeMessageType.Event"/>.</summary>
    event EventHandler<BridgeEventArgs>? EventReceived;

    /// <summary>
    /// Sends a request to the editor and awaits its typed response.
    /// </summary>
    /// <typeparam name="TRequest">Payload type sent to the editor.</typeparam>
    /// <typeparam name="TResponse">Payload type expected back.</typeparam>
    Task<TResponse> InvokeAsync<TRequest, TResponse>(
        string method,
        TRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a fire-and-forget event to the editor.</summary>
    Task PublishAsync<TPayload>(string method, TPayload payload, CancellationToken cancellationToken = default);

    /// <summary>Registers a handler the host exposes for editor-initiated requests.</summary>
    void RegisterHandler(string method, EditorRequestHandler handler);
}

/// <summary>Handler invoked when the editor calls a host-registered method.</summary>
/// <param name="payloadJson">Raw JSON payload from the editor.</param>
/// <returns>JSON payload to return, or null for an empty response.</returns>
public delegate Task<string?> EditorRequestHandler(string? payloadJson, CancellationToken cancellationToken);

/// <summary>Carries an editor-originated event to host subscribers.</summary>
public sealed class BridgeEventArgs : EventArgs
{
    public BridgeEventArgs(string method, string? payloadJson)
    {
        Method = method;
        PayloadJson = payloadJson;
    }

    public string Method { get; }
    public string? PayloadJson { get; }
}
