using System.Diagnostics;

namespace WebsiteBuilder.App.Services;

public interface IPreviewBrowser
{
    void Open(Uri uri);
}

public sealed class ShellPreviewBrowser : IPreviewBrowser
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsLoopback || uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException("Preview navigation is restricted to local HTTP URLs.");
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
