using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebsiteBuilder.Bridge;

/// <summary>The role of a <see cref="BridgeMessage"/> in the JSON-RPC exchange.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BridgeMessageType
{
    /// <summary>Caller asks the other side to run a method and awaits a response.</summary>
    Request,

    /// <summary>The reply to a <see cref="Request"/>, correlated by <see cref="BridgeMessage.Id"/>.</summary>
    Response,

    /// <summary>A fire-and-forget notification with no expected reply.</summary>
    Event,
}

/// <summary>
/// The wire envelope exchanged over WebView2 <c>postMessage</c> in both
/// directions. Both the C# host and the TypeScript client serialize to this
/// exact shape so either side can add new methods without touching plumbing.
/// </summary>
public sealed class BridgeMessage
{
    /// <summary>Correlation id; pairs a response with its request. Empty for events.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public BridgeMessageType Type { get; set; }

    /// <summary>The method/event name being invoked (e.g. "editor.ready", "project.load").</summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>Opaque payload; interpreted per-method. Null for empty payloads.</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    /// <summary>Present only on a failed <see cref="BridgeMessageType.Response"/>.</summary>
    [JsonPropertyName("error")]
    public BridgeError? Error { get; set; }
}

/// <summary>Structured error returned when a request handler throws.</summary>
public sealed class BridgeError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "error";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
