using System.Net;
using System.Net.Sockets;
using System.Text;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;
using Xunit;

namespace WebsiteBuilder.App.Tests;

public sealed class PreviewServiceTests
{
    [Fact]
    public async Task Start_ServesCurrentProject_AndStopCleansSnapshot()
    {
        var testRoot = TestDirectory();
        try
        {
            var projects = new ProjectService();
            var project = projects.New("Preview Test");
            project.Pages[0].Root.Children.Add(new ElementNode
            {
                Id = "preview-heading",
                Type = ElementTypes.Heading,
                Text = "Preview works",
            });
            var browser = new RecordingBrowser();
            var exporter = new ProjectExportService(new AssetService(new AssetImportOptions()));
            await using var preview = new PreviewService(
                projects,
                exporter,
                browser,
                new PreviewOptions { RootDirectory = testRoot });

            var url = await preview.StartAsync();
            var runDirectory = Assert.IsType<string>(preview.RunDirectory);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Preview works", html, StringComparison.Ordinal);
            Assert.Equal(url, browser.OpenedUrl);
            Assert.True(preview.IsRunning);
            Assert.True(Directory.Exists(runDirectory));

            await preview.StopAsync();

            Assert.False(preview.IsRunning);
            Assert.Null(preview.Url);
            Assert.False(Directory.Exists(runDirectory));
        }
        finally
        {
            DeleteTestDirectory(testRoot);
        }
    }

    [Fact]
    public async Task Server_ServesExtensionlessRoute_WithSecurityHeaders()
    {
        var testRoot = TestDirectory();
        Directory.CreateDirectory(testRoot);
        await File.WriteAllTextAsync(Path.Combine(testRoot, "index.html"), "home");
        await File.WriteAllTextAsync(Path.Combine(testRoot, "about.html"), "about");
        await using var server = new LocalPreviewServer();
        try
        {
            await server.StartAsync(testRoot);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync(new Uri(server.Url!, "about"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("about", await response.Content.ReadAsStringAsync());
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
            Assert.Contains("default-src 'self'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestDirectory(testRoot);
        }
    }

    [Fact]
    public async Task Server_RejectsEncodedTraversal()
    {
        var parent = TestDirectory();
        var root = Path.Combine(parent, "site");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "home");
        await File.WriteAllTextAsync(Path.Combine(parent, "secret.txt"), "not public");
        await using var server = new LocalPreviewServer();
        try
        {
            await server.StartAsync(root);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Url!.Port);
            await using var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes(
                $"GET /%2e%2e/secret.txt HTTP/1.1\r\nHost: 127.0.0.1:{server.Url.Port}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(request);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            var response = await reader.ReadToEndAsync();

            Assert.StartsWith("HTTP/1.1 404 Not Found", response, StringComparison.Ordinal);
            Assert.DoesNotContain("not public", response, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestDirectory(parent);
        }
    }

    private static string TestDirectory() =>
        Path.Combine(Path.GetTempPath(), $"LikhaPreviewTests-{Guid.NewGuid():N}");

    private static void DeleteTestDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class RecordingBrowser : IPreviewBrowser
    {
        public Uri? OpenedUrl { get; private set; }

        public void Open(Uri uri) => OpenedUrl = uri;
    }
}
