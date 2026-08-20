namespace WebsiteBuilder.App.Bridge;

/// <summary>Payload returned by the host's <c>host.getInfo</c> method.</summary>
public sealed record HostInfo(string Name, string Version, string Platform);

/// <summary>Request payload for the demonstration <c>editor.echo</c> round-trip.</summary>
public sealed record EchoRequest(string Message);

/// <summary>Response payload for <c>editor.echo</c>.</summary>
public sealed record EchoResponse(string Reply);
