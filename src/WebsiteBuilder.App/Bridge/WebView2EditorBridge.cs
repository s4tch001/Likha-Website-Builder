using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using WebsiteBuilder.Bridge;

namespace WebsiteBuilder.App.Bridge;

/// <summary>
/// Concrete <see cref="IEditorBridge"/> implemented over the WebView2
/// <c>postMessage</c> channel. Each call is a JSON-RPC <see cref="BridgeMessage"/>:
/// requests carry a correlation id and are matched to their response; events are
/// fire-and-forget. The same envelope shape is used in both directions, so the
/// host and the editor are symmetric peers.
/// </summary>
public sealed class WebView2EditorBridge : IEditorBridge, IDisposable
{
    private readonly CoreWebView2 _core;
    private readonly Dispatcher _dispatcher;
    private readonly JsonSerializerOptions _json;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<BridgeMessage>> _pending = new();
    private readonly ConcurrentDictionary<string, EditorRequestHandler> _handlers = new();

    public WebView2EditorBridge(CoreWebView2 core, Dispatcher dispatcher, JsonSerializerOptions json)
    {
        _core = core;
        _dispatcher = dispatcher;
        _json = json;
        _core.WebMessageReceived += OnWebMessageReceived;
    }

    /// <inheritdoc />
    public bool IsReady { get; internal set; }

    /// <inheritdoc />
    public event EventHandler<BridgeEventArgs>? EventReceived;

    /// <inheritdoc />
    public void RegisterHandler(string method, EditorRequestHandler handler)
        => _handlers[method] = handler;

    /// <inheritdoc />
    public async Task<TResponse> InvokeAsync<TRequest, TResponse>(
        string method, TRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var message = new BridgeMessage
        {
            Id = id,
            Type = BridgeMessageType.Request,
            Method = method,
            Payload = JsonSerializer.SerializeToElement(request, _json),
        };

        var tcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await using var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() =>
            {
                if (_pending.TryRemove(id, out var pending))
                {
                    pending.TrySetCanceled(cancellationToken);
                }
            })
            : default;

        await PostAsync(message).ConfigureAwait(false);

        var response = await tcs.Task.ConfigureAwait(false);
        if (response.Error is not null)
        {
            throw new EditorBridgeException(response.Error.Code, response.Error.Message);
        }

        return Deserialize<TResponse>(response.Payload);
    }

    /// <inheritdoc />
    public Task PublishAsync<TPayload>(string method, TPayload payload, CancellationToken cancellationToken = default)
    {
        var message = new BridgeMessage
        {
            Id = string.Empty,
            Type = BridgeMessageType.Event,
            Method = method,
            Payload = JsonSerializer.SerializeToElement(payload, _json),
        };

        return PostAsync(message);
    }

    private Task PostAsync(BridgeMessage message)
    {
        var json = JsonSerializer.Serialize(message, _json);

        if (_dispatcher.CheckAccess())
        {
            _core.PostWebMessageAsJson(json);
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(() => _core.PostWebMessageAsJson(json)).Task;
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        BridgeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<BridgeMessage>(e.WebMessageAsJson, _json);
        }
        catch (JsonException)
        {
            return;
        }

        if (message is null)
        {
            return;
        }

        switch (message.Type)
        {
            case BridgeMessageType.Response:
                if (_pending.TryRemove(message.Id, out var tcs))
                {
                    tcs.TrySetResult(message);
                }

                break;

            case BridgeMessageType.Event:
                EventReceived?.Invoke(this, new BridgeEventArgs(message.Method, message.Payload?.GetRawText()));
                break;

            case BridgeMessageType.Request:
                await HandleEditorRequestAsync(message).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleEditorRequestAsync(BridgeMessage request)
    {
        BridgeMessage response;

        if (_handlers.TryGetValue(request.Method, out var handler))
        {
            try
            {
                var resultJson = await handler(request.Payload?.GetRawText(), CancellationToken.None)
                    .ConfigureAwait(false);

                response = new BridgeMessage
                {
                    Id = request.Id,
                    Type = BridgeMessageType.Response,
                    Method = request.Method,
                    Payload = resultJson is null ? null : JsonSerializer.Deserialize<JsonElement>(resultJson),
                };
            }
            catch (Exception ex)
            {
                response = ErrorResponse(request, "handler_error", ex.Message);
            }
        }
        else
        {
            response = ErrorResponse(request, "not_found", $"No host handler registered for '{request.Method}'.");
        }

        await PostAsync(response).ConfigureAwait(false);
    }

    private static BridgeMessage ErrorResponse(BridgeMessage request, string code, string message) => new()
    {
        Id = request.Id,
        Type = BridgeMessageType.Response,
        Method = request.Method,
        Error = new BridgeError { Code = code, Message = message },
    };

    private TResponse Deserialize<TResponse>(JsonElement? payload)
    {
        if (payload is null
            || payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return default!;
        }

        return payload.Value.Deserialize<TResponse>(_json)!;
    }

    public void Dispose() => _core.WebMessageReceived -= OnWebMessageReceived;
}
