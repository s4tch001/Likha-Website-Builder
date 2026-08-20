using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WebsiteBuilder.App.Services;

/// <summary>A bounded, loopback-only static file server for generated preview snapshots.</summary>
public sealed class LocalPreviewServer : IAsyncDisposable
{
    private const int MaxRequestHeaderBytes = 8 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _clientLimit = new(8, 8);
    private readonly ConcurrentDictionary<int, Task> _clientTasks = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptLoop;
    private string? _root;
    private string? _rootPrefix;
    private int _taskId;

    public Uri? Url { get; private set; }

    public Task StartAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (_listener is not null)
        {
            throw new InvalidOperationException("The preview server is already running.");
        }

        var root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Preview root not found: {root}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(backlog: 16);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        _root = root;
        _rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        _listener = listener;
        _lifetime = new CancellationTokenSource();
        Url = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute);
        _acceptLoop = AcceptLoopAsync(listener, _lifetime.Token);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await _clientLimit.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }

                var id = Interlocked.Increment(ref _taskId);
                var task = HandleClientSafelyAsync(client, cancellationToken);
                _clientTasks.TryAdd(id, task);
                _ = task.ContinueWith(
                    completedTask =>
                    {
                        _clientTasks.TryRemove(id, out _);
                        _clientLimit.Release();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleClientSafelyAsync(TcpClient client, CancellationToken lifetimeToken)
    {
        using (client)
        using (var requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken))
        {
            requestLifetime.CancelAfter(RequestTimeout);
            try
            {
                client.NoDelay = true;
                await HandleClientAsync(client.GetStream(), requestLifetime.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
            {
                // A disconnected or slow browser request must not affect the listener.
            }
        }
    }

    private async Task HandleClientAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = await ReadRequestHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
        if (header is null)
        {
            await WriteErrorAsync(stream, 400, "Bad Request", cancellationToken).ConfigureAwait(false);
            return;
        }

        var requestLineEnd = header.IndexOf("\r\n", StringComparison.Ordinal);
        var requestLine = requestLineEnd >= 0 ? header[..requestLineEnd] : header;
        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
        {
            await WriteErrorAsync(stream, 400, "Bad Request", cancellationToken).ConfigureAwait(false);
            return;
        }

        var isHead = parts[0].Equals("HEAD", StringComparison.Ordinal);
        if (!isHead && !parts[0].Equals("GET", StringComparison.Ordinal))
        {
            await WriteErrorAsync(stream, 405, "Method Not Allowed", cancellationToken, "Allow: GET, HEAD\r\n")
                .ConfigureAwait(false);
            return;
        }

        var filePath = ResolveRequestPath(parts[1]);
        if (filePath is null)
        {
            await WriteErrorAsync(stream, 404, "Not Found", cancellationToken).ConfigureAwait(false);
            return;
        }

        var fileInfo = new FileInfo(filePath);
        var responseHeader = BuildHeaders(
            200,
            "OK",
            fileInfo.Length,
            ContentTypeFor(fileInfo.Extension));
        await stream.WriteAsync(Encoding.ASCII.GetBytes(responseHeader), cancellationToken).ConfigureAwait(false);
        if (!isHead)
        {
            await using var file = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadRequestHeaderAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxRequestHeaderBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length, buffer.Length - length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            length += read;
            for (var i = Math.Max(0, length - read - 3); i <= length - 4; i++)
            {
                if (buffer[i] == '\r' && buffer[i + 1] == '\n'
                    && buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                {
                    return Encoding.ASCII.GetString(buffer, 0, i + 4);
                }
            }
        }

        return null;
    }

    private string? ResolveRequestPath(string requestTarget)
    {
        if (_root is null || _rootPrefix is null
            || requestTarget.Length is 0 or > 2048
            || !requestTarget.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var queryIndex = requestTarget.IndexOf('?');
        var encodedPath = queryIndex >= 0 ? requestTarget[..queryIndex] : requestTarget;
        string decodedPath;
        try
        {
            decodedPath = Uri.UnescapeDataString(encodedPath);
        }
        catch (UriFormatException)
        {
            return null;
        }

        if (decodedPath.IndexOfAny(['\0', '\\']) >= 0)
        {
            return null;
        }

        var relative = decodedPath.TrimStart('/');
        if (relative.Length == 0 || decodedPath.EndsWith("/", StringComparison.Ordinal))
        {
            relative += "index.html";
        }

        var candidates = Path.HasExtension(relative)
            ? new[] { relative }
            : new[] { relative, relative + ".html", relative + "/index.html" };
        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(Path.Combine(
                _root,
                candidate.Replace('/', Path.DirectorySeparatorChar)));
            if ((fullPath.Equals(_root, StringComparison.OrdinalIgnoreCase)
                 || fullPath.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
                && File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream",
    };

    private static async Task WriteErrorAsync(
        NetworkStream stream,
        int status,
        string reason,
        CancellationToken cancellationToken,
        string additionalHeaders = "")
    {
        var body = Encoding.UTF8.GetBytes($"{status} {reason}\n");
        var headers = BuildHeaders(status, reason, body.Length, "text/plain; charset=utf-8", additionalHeaders);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildHeaders(
        int status,
        string reason,
        long contentLength,
        string contentType,
        string additionalHeaders = "") =>
        $"HTTP/1.1 {status} {reason}\r\n" +
        $"Content-Length: {contentLength}\r\n" +
        $"Content-Type: {contentType}\r\n" +
        "Cache-Control: no-store\r\n" +
        "Content-Security-Policy: default-src 'self'; img-src 'self' data:; media-src 'self'; font-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'\r\n" +
        "Cross-Origin-Resource-Policy: same-origin\r\n" +
        "Referrer-Policy: no-referrer\r\n" +
        "X-Content-Type-Options: nosniff\r\n" +
        additionalHeaders +
        "Connection: close\r\n\r\n";

    public async ValueTask DisposeAsync()
    {
        var listener = Interlocked.Exchange(ref _listener, null);
        var lifetime = Interlocked.Exchange(ref _lifetime, null);
        var acceptLoop = Interlocked.Exchange(ref _acceptLoop, null);
        Url = null;
        if (listener is null || lifetime is null)
        {
            return;
        }

        lifetime.Cancel();
        listener.Stop();
        if (acceptLoop is not null)
        {
            try
            {
                await acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        var clients = _clientTasks.Values.ToArray();
        if (clients.Length > 0)
        {
            await Task.WhenAll(clients).ConfigureAwait(false);
        }

        lifetime.Dispose();
    }
}
