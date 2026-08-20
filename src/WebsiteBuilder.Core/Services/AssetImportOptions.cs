namespace WebsiteBuilder.Core.Services;

/// <summary>Strongly typed limits for the local asset import boundary.</summary>
public sealed class AssetImportOptions
{
    public long MaxImageBytes { get; init; } = 25L * 1024 * 1024;
    public long MaxSvgBytes { get; init; } = 5L * 1024 * 1024;
    public long MaxVideoBytes { get; init; } = 250L * 1024 * 1024;
    public long MaxAudioBytes { get; init; } = 100L * 1024 * 1024;
    public long MaxFontBytes { get; init; } = 20L * 1024 * 1024;
    public long MaxDocumentBytes { get; init; } = 25L * 1024 * 1024;
}
