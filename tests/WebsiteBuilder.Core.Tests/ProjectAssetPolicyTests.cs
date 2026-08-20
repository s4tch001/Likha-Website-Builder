using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

public sealed class ProjectAssetPolicyTests
{
    [Fact]
    public void CountReferences_IncludesAttributesAndFontStyles()
    {
        var project = Project.CreateDefault();
        var asset = new ProjectAsset { Id = "font-123", RelativePath = "Assets/font.woff2" };
        project.Pages[0].Root.Children.Add(new ElementNode
        {
            Id = "child",
            Attributes = { ["src"] = asset.RelativePath },
            Styles = { ["font-family"] = $"'{ProjectAssetPolicy.FontFamily(asset)}'" },
        });

        Assert.Equal(2, ProjectAssetPolicy.CountReferences(project, asset));
        Assert.Equal("LikhaAsset_font123", ProjectAssetPolicy.FontFamily(asset));
    }

    [Fact]
    public async Task CopyFileAsync_StreamsAndReplacesDestination()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wb-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.bin");
            var destination = Path.Combine(directory, "out", "asset.bin");
            await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
            await AtomicFileWriter.CopyFileAsync(source, destination);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(destination));

            await File.WriteAllBytesAsync(source, [9, 8]);
            await AtomicFileWriter.CopyFileAsync(source, destination);
            Assert.Equal(new byte[] { 9, 8 }, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
