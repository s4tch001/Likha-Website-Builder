using System.Text;

namespace WebsiteBuilder.Core.Services;

/// <summary>
/// Writes UTF-8 text through a same-directory temporary file and atomically
/// replaces the destination. Existing files receive a rolling <c>.bak</c> copy.
/// </summary>
public static class AtomicFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Atomically writes text and optionally keeps the previous file as a backup.</summary>
    public static async Task WriteAllTextAsync(
        string path,
        string contents,
        bool createBackup = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The destination must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream, Utf8NoBom))
            {
                await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (!File.Exists(fullPath))
            {
                File.Move(temporaryPath, fullPath);
                return;
            }

            var backupPath = createBackup ? fullPath + ".bak" : null;
            try
            {
                File.Replace(temporaryPath, fullPath, backupPath, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithMove(temporaryPath, fullPath, backupPath);
            }
            catch (IOException) when (File.Exists(temporaryPath))
            {
                ReplaceWithMove(temporaryPath, fullPath, backupPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ReplaceWithMove(string temporaryPath, string fullPath, string? backupPath)
    {
        if (backupPath is not null)
        {
            File.Copy(fullPath, backupPath, overwrite: true);
        }

        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
