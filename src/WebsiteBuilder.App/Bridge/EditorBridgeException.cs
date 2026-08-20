namespace WebsiteBuilder.App.Bridge;

/// <summary>Raised when the editor returns an error response to a host request.</summary>
public sealed class EditorBridgeException : Exception
{
    public EditorBridgeException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Machine-readable error code supplied by the editor.</summary>
    public string Code { get; }
}
