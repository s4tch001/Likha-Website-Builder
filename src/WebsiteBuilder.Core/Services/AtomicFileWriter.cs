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

    /// <summary>Streams a file through a same-directory temporary path, then atomically publishes it.</summary>
    public static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool createBackup = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var sourceFullPath = Path.GetFullPath(sourcePath);
        var destinationFullPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = Path.GetDirectoryName(destinationFullPath)
            ?? throw new InvalidOperationException("The destination must have a parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destinationFullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var input = new FileStream(
                sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await input.CopyToAsync(output, 64 * 1024, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            if (!File.Exists(destinationFullPath))
            {
                File.Move(temporaryPath, destinationFullPath);
            }
            else
            {
                var backupPath = createBackup ? destinationFullPath + ".bak" : null;
                try
                {
                    File.Replace(temporaryPath, destinationFullPath, backupPath, ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithMove(temporaryPath, destinationFullPath, backupPath);
                }
                catch (IOException) when (File.Exists(temporaryPath))
                {
                    ReplaceWithMove(temporaryPath, destinationFullPath, backupPath);
                }
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
