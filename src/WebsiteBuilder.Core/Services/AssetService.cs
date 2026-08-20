using System.Buffers;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Services;

/// <summary>
/// Secure project-local asset storage. It applies an extension allowlist, byte
/// limits, content signatures, safe SVG/text parsing, randomized stored names,
/// containment checks, and an atomic temp-file commit.
/// </summary>
public sealed class AssetService(AssetImportOptions options) : IAssetService
{
    public const string AssetsFolderName = "Assets";

    private const int HeaderLength = 4096;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly AssetImportOptions _options = ValidateOptions(options);

    private static readonly IReadOnlyDictionary<string, AssetDescriptor> Descriptors =
        new Dictionary<string, AssetDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = new(AssetKinds.Image, "image/png", Signature.Png),
            [".jpg"] = new(AssetKinds.Image, "image/jpeg", Signature.Jpeg),
            [".jpeg"] = new(AssetKinds.Image, "image/jpeg", Signature.Jpeg),
            [".gif"] = new(AssetKinds.Image, "image/gif", Signature.Gif),
            [".webp"] = new(AssetKinds.Image, "image/webp", Signature.Webp),
            [".bmp"] = new(AssetKinds.Image, "image/bmp", Signature.Bmp),
            [".avif"] = new(AssetKinds.Image, "image/avif", Signature.Avif),
            [".ico"] = new(AssetKinds.Icon, "image/x-icon", Signature.Ico),
            [".svg"] = new(AssetKinds.Svg, "image/svg+xml", Signature.Svg),
            [".mp4"] = new(AssetKinds.Video, "video/mp4", Signature.IsoMedia),
            [".mov"] = new(AssetKinds.Video, "video/quicktime", Signature.IsoMedia),
            [".webm"] = new(AssetKinds.Video, "video/webm", Signature.Webm),
            [".ogv"] = new(AssetKinds.Video, "video/ogg", Signature.Ogg),
            [".mp3"] = new(AssetKinds.Audio, "audio/mpeg", Signature.Mp3),
            [".wav"] = new(AssetKinds.Audio, "audio/wav", Signature.Wav),
            [".ogg"] = new(AssetKinds.Audio, "audio/ogg", Signature.Ogg),
            [".m4a"] = new(AssetKinds.Audio, "audio/mp4", Signature.IsoMedia),
            [".woff"] = new(AssetKinds.Font, "font/woff", Signature.Woff),
            [".woff2"] = new(AssetKinds.Font, "font/woff2", Signature.Woff2),
            [".ttf"] = new(AssetKinds.Font, "font/ttf", Signature.Ttf),
            [".otf"] = new(AssetKinds.Font, "font/otf", Signature.Otf),
            [".pdf"] = new(AssetKinds.Document, "application/pdf", Signature.Pdf),
            [".txt"] = new(AssetKinds.Document, "text/plain", Signature.Text),
            [".md"] = new(AssetKinds.Document, "text/markdown", Signature.Text),
            [".json"] = new(AssetKinds.Document, "application/json", Signature.Json),
        };

    public async Task<AssetImportResult> ImportAsync(
        Project project,
        string projectDirectory,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!TryGetProjectPaths(projectDirectory, out _, out var assetsDirectory))
        {
            return AssetImportResult.Fail(AssetImportFailure.InvalidPath, "The project folder is invalid.");
        }

        string sourceFullPath;
        try
        {
            sourceFullPath = Path.GetFullPath(sourcePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return AssetImportResult.Fail(AssetImportFailure.InvalidPath, "The selected file path is invalid.");
        }

        if (!File.Exists(sourceFullPath))
        {
            return AssetImportResult.Fail(AssetImportFailure.FileNotFound, "The selected file no longer exists.");
        }

        var extension = Path.GetExtension(sourceFullPath).ToLowerInvariant();
        if (!Descriptors.TryGetValue(extension, out var descriptor))
        {
            return AssetImportResult.Fail(AssetImportFailure.UnsupportedType, "This file type is not supported.");
        }

        try
        {
            if ((File.GetAttributes(sourceFullPath) & FileAttributes.ReparsePoint) != 0)
            {
                return AssetImportResult.Fail(AssetImportFailure.UnsafeFile, "Linked files cannot be imported.");
            }

            Directory.CreateDirectory(assetsDirectory);
            if ((File.GetAttributes(assetsDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                return AssetImportResult.Fail(AssetImportFailure.UnsafeFile, "The Assets folder cannot be a link.");
            }

            var maxBytes = MaximumBytes(descriptor.Kind);
            await using var input = new FileStream(
                sourceFullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            if (input.Length == 0)
            {
                return AssetImportResult.Fail(AssetImportFailure.InvalidContent, "Empty files cannot be imported.");
            }

            if (input.Length > maxBytes)
            {
                return AssetImportResult.Fail(
                    AssetImportFailure.FileTooLarge,
                    $"The file exceeds the {FormatLimit(maxBytes)} limit for {descriptor.Kind.ToLowerInvariant()} assets.");
            }

            var validation = await ValidateContentAsync(input, descriptor.Signature, cancellationToken)
                .ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return AssetImportResult.Fail(AssetImportFailure.InvalidContent, validation.Message);
            }

            input.Position = 0;
            var id = Guid.NewGuid().ToString("N");
            var storedFileName = id + extension;
            var destination = Path.GetFullPath(Path.Combine(assetsDirectory, storedFileName));
            var tempPath = Path.GetFullPath(Path.Combine(assetsDirectory, $".{id}.importing"));
            if (!IsContained(assetsDirectory, destination) || !IsContained(assetsDirectory, tempPath))
            {
                return AssetImportResult.Fail(AssetImportFailure.InvalidPath, "The asset destination is invalid.");
            }

            try
            {
                var copy = await CopyAndHashAsync(input, tempPath, maxBytes, cancellationToken)
                    .ConfigureAwait(false);
                File.Move(tempPath, destination, overwrite: false);

                var asset = new ProjectAsset
                {
                    Id = id,
                    Name = SafeDisplayName(Path.GetFileName(sourceFullPath)),
                    StoredFileName = storedFileName,
                    RelativePath = $"{AssetsFolderName}/{storedFileName}",
                    Kind = descriptor.Kind,
                    MediaType = descriptor.MediaType,
                    SizeBytes = copy.SizeBytes,
                    Sha256 = copy.Sha256,
                    ImportedUtc = DateTimeOffset.UtcNow,
                };
                project.Assets.Add(asset);
                return AssetImportResult.Success(asset);
            }
            finally
            {
                TryDeleteTemporaryFile(tempPath);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return AssetImportResult.Fail(AssetImportFailure.StorageError, "The asset could not be stored.");
        }
    }

    public bool TryGetFullPath(string projectDirectory, ProjectAsset asset, out string fullPath)
    {
        ArgumentNullException.ThrowIfNull(asset);
        fullPath = string.Empty;

        if (!TryGetProjectPaths(projectDirectory, out _, out var assetsDirectory)
            || string.IsNullOrWhiteSpace(asset.StoredFileName)
            || string.IsNullOrWhiteSpace(asset.RelativePath)
            || !string.Equals(asset.StoredFileName, Path.GetFileName(asset.StoredFileName), StringComparison.Ordinal)
            || !string.Equals(
                asset.RelativePath.Replace('\\', '/'),
                $"{AssetsFolderName}/{asset.StoredFileName}",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (Directory.Exists(assetsDirectory)
            && (File.GetAttributes(assetsDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(assetsDirectory, asset.StoredFileName));
        if (!IsContained(assetsDirectory, candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    public Task<AssetDeleteResult> DeleteAsync(
        Project project,
        string projectDirectory,
        ProjectAsset asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(asset);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetFullPath(projectDirectory, asset, out var fullPath))
        {
            return Task.FromResult(AssetDeleteResult.Fail("The asset path is invalid."));
        }

        try
        {
            if (File.Exists(fullPath))
            {
                if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                {
                    return Task.FromResult(AssetDeleteResult.Fail("Linked asset files cannot be deleted here."));
                }

                File.Delete(fullPath);
            }

            project.Assets.RemoveAll(candidate =>
                string.Equals(candidate.Id, asset.Id, StringComparison.Ordinal));
            return Task.FromResult(AssetDeleteResult.Success());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Task.FromResult(AssetDeleteResult.Fail("The asset could not be deleted."));
        }
    }

    private long MaximumBytes(string kind) => kind switch
    {
        AssetKinds.Image or AssetKinds.Icon => _options.MaxImageBytes,
        AssetKinds.Svg => _options.MaxSvgBytes,
        AssetKinds.Video => _options.MaxVideoBytes,
        AssetKinds.Audio => _options.MaxAudioBytes,
        AssetKinds.Font => _options.MaxFontBytes,
        _ => _options.MaxDocumentBytes,
    };

    private static async Task<ContentValidation> ValidateContentAsync(
        FileStream input,
        Signature signature,
        CancellationToken cancellationToken)
    {
        if (signature is Signature.Text or Signature.Json or Signature.Svg)
        {
            input.Position = 0;
            using var reader = new StreamReader(input, StrictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            string text;
            try
            {
                text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DecoderFallbackException)
            {
                return ContentValidation.Invalid("Text assets must use valid UTF-8 encoding.");
            }

            if (text.Any(character => character == '\0'
                || (char.IsControl(character) && character is not '\r' and not '\n' and not '\t')))
            {
                return ContentValidation.Invalid("The text asset contains unsupported control characters.");
            }

            if (signature == Signature.Json)
            {
                try
                {
                    using var _ = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
                }
                catch (JsonException)
                {
                    return ContentValidation.Invalid("The JSON asset is not valid JSON.");
                }
            }

            return signature == Signature.Svg ? ValidateSvg(text) : ContentValidation.Valid();
        }

        input.Position = 0;
        var length = (int)Math.Min(HeaderLength, input.Length);
        var header = new byte[length];
        var read = 0;
        while (read < header.Length)
        {
            var count = await input.ReadAsync(header.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        return MatchesSignature(header.AsSpan(0, read), signature)
            ? ContentValidation.Valid()
            : ContentValidation.Invalid("The file contents do not match its extension.");
    }

    private static ContentValidation ValidateSvg(string text)
    {
        var prohibitedElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "foreignObject", "iframe", "object", "embed",
            "animate", "animateMotion", "animateTransform", "set",
        };

        try
        {
            using var stringReader = new StringReader(text);
            using var reader = XmlReader.Create(stringReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = text.Length,
            });

            var sawRoot = false;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!sawRoot)
                {
                    sawRoot = true;
                    if (!reader.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                    {
                        return ContentValidation.Invalid("The SVG document must have an svg root element.");
                    }
                }

                if (prohibitedElements.Contains(reader.LocalName))
                {
                    return ContentValidation.Invalid("The SVG contains active or embedded content.");
                }

                if (!reader.HasAttributes)
                {
                    continue;
                }

                while (reader.MoveToNextAttribute())
                {
                    var value = reader.Value.Trim();
                    if (reader.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("data:text/html", StringComparison.OrdinalIgnoreCase)
                        || (value.Contains("url(", StringComparison.OrdinalIgnoreCase)
                            && !IsInternalSvgUrl(value))
                        || (reader.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase)
                            && (value.Contains("url(", StringComparison.OrdinalIgnoreCase)
                                || value.Contains("expression(", StringComparison.OrdinalIgnoreCase)))
                        || (reader.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase)
                            && value.Length > 0
                            && !value.StartsWith('#')))
                    {
                        return ContentValidation.Invalid("The SVG contains an unsafe reference or event handler.");
                    }
                }

                reader.MoveToElement();
            }

            return sawRoot
                ? ContentValidation.Valid()
                : ContentValidation.Invalid("The SVG document is empty.");
        }
        catch (XmlException)
        {
            return ContentValidation.Invalid("The SVG document is not valid XML.");
        }
    }

    private static bool MatchesSignature(ReadOnlySpan<byte> bytes, Signature signature) => signature switch
    {
        Signature.Png => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        Signature.Jpeg => bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
        Signature.Gif => StartsWithAscii(bytes, "GIF87a") || StartsWithAscii(bytes, "GIF89a"),
        Signature.Webp => StartsWithAscii(bytes, "RIFF") && AsciiAt(bytes, 8, "WEBP"),
        Signature.Bmp => StartsWithAscii(bytes, "BM"),
        Signature.Avif => AsciiAt(bytes, 4, "ftyp")
            && (ContainsAscii(bytes[..Math.Min(bytes.Length, 32)], "avif")
                || ContainsAscii(bytes[..Math.Min(bytes.Length, 32)], "avis")),
        Signature.Ico => bytes.StartsWith(new byte[] { 0x00, 0x00, 0x01, 0x00 }),
        Signature.IsoMedia => AsciiAt(bytes, 4, "ftyp"),
        Signature.Webm => bytes.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
        Signature.Ogg => StartsWithAscii(bytes, "OggS"),
        Signature.Mp3 => StartsWithAscii(bytes, "ID3")
            || (bytes.Length >= 2 && bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0),
        Signature.Wav => StartsWithAscii(bytes, "RIFF") && AsciiAt(bytes, 8, "WAVE"),
        Signature.Woff => StartsWithAscii(bytes, "wOFF"),
        Signature.Woff2 => StartsWithAscii(bytes, "wOF2"),
        Signature.Ttf => bytes.StartsWith(new byte[] { 0x00, 0x01, 0x00, 0x00 })
            || StartsWithAscii(bytes, "true"),
        Signature.Otf => StartsWithAscii(bytes, "OTTO"),
        Signature.Pdf => StartsWithAscii(bytes, "%PDF-"),
        _ => false,
    };

    private static async Task<CopyResult> CopyAndHashAsync(
        Stream input,
        string tempPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long total = 0;
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > maxBytes)
                {
                    throw new IOException("The source file changed during import.");
                }

                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            return new CopyResult(total, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static bool TryGetProjectPaths(
        string projectDirectory,
        out string projectRoot,
        out string assetsDirectory)
    {
        projectRoot = string.Empty;
        assetsDirectory = string.Empty;
        try
        {
            projectRoot = Path.GetFullPath(projectDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(projectRoot))
            {
                return false;
            }

            assetsDirectory = Path.GetFullPath(Path.Combine(projectRoot, AssetsFolderName));
            return IsContained(projectRoot, assetsDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException
            or IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    private static bool IsContained(string directory, string candidate)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInternalSvgUrl(string value)
    {
        var compact = string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
        return compact.StartsWith("url(#", StringComparison.OrdinalIgnoreCase)
            && compact.EndsWith(')')
            && compact.Count(character => character == '(') == 1
            && compact.Count(character => character == ')') == 1;
    }

    private static AssetImportOptions ValidateOptions(AssetImportOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.MaxImageBytes <= 0 || value.MaxSvgBytes <= 0 || value.MaxVideoBytes <= 0
            || value.MaxAudioBytes <= 0 || value.MaxFontBytes <= 0 || value.MaxDocumentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "All asset byte limits must be positive.");
        }

        return value;
    }

    private static string SafeDisplayName(string fileName)
    {
        var normalized = fileName.Normalize(NormalizationForm.FormC);
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(normalized
            .Select(character => char.IsControl(character) || invalid.Contains(character) ? '-' : character)
            .ToArray())
            .Trim(' ', '.');
        if (safe.Length == 0)
        {
            safe = "Asset";
        }

        return safe.Length <= 160 ? safe : safe[..160];
    }

    private static string FormatLimit(long bytes) =>
        $"{bytes / (1024 * 1024)} MB";

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup. The dot-prefixed, random temp name is never added to metadata.
        }
    }

    private static bool StartsWithAscii(ReadOnlySpan<byte> bytes, string value) =>
        AsciiAt(bytes, 0, value);

    private static bool AsciiAt(ReadOnlySpan<byte> bytes, int offset, string value)
    {
        if (offset < 0 || bytes.Length - offset < value.Length)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (bytes[offset + index] != value[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsAscii(ReadOnlySpan<byte> bytes, string value)
    {
        for (var index = 0; index <= bytes.Length - value.Length; index++)
        {
            if (AsciiAt(bytes, index, value))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record AssetDescriptor(string Kind, string MediaType, Signature Signature);

    private readonly record struct ContentValidation(bool IsValid, string Message)
    {
        public static ContentValidation Valid() => new(true, string.Empty);
        public static ContentValidation Invalid(string message) => new(false, message);
    }

    private readonly record struct CopyResult(long SizeBytes, string Sha256);

    private enum Signature
    {
        Png, Jpeg, Gif, Webp, Bmp, Avif, Ico, Svg, IsoMedia, Webm, Ogg, Mp3, Wav,
        Woff, Woff2, Ttf, Otf, Pdf, Text, Json,
    }
}
