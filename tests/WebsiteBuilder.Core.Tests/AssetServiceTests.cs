using System.Security.Cryptography;
using System.Text;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

/// <summary>Security and storage-contract tests for the Phase 13a import boundary.</summary>
public sealed class AssetServiceTests : IDisposable
{
    private static readonly byte[] ValidPng =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01, 0x02, 0x03];

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wb_assets_{Guid.NewGuid():N}");
    private readonly string _projectDirectory;
    private readonly string _sourceDirectory;

    public AssetServiceTests()
    {
        _projectDirectory = Path.Combine(_root, "project");
        _sourceDirectory = Path.Combine(_root, "source");
        Directory.CreateDirectory(_projectDirectory);
        Directory.CreateDirectory(_sourceDirectory);
    }

    [Fact]
    public async Task Import_ValidPng_UsesRandomManagedNameAndPersistsMetadata()
    {
        var source = WriteSource("My Logo.png", ValidPng);
        var project = Project.CreateDefault("Assets");
        var service = CreateService();

        var result = await service.ImportAsync(project, _projectDirectory, source);

        Assert.True(result.IsSuccess);
        var asset = Assert.IsType<ProjectAsset>(result.Asset);
        Assert.Equal("My Logo.png", asset.Name);
        Assert.Equal(AssetKinds.Image, asset.Kind);
        Assert.Equal("image/png", asset.MediaType);
        Assert.Equal(ValidPng.Length, asset.SizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(ValidPng)).ToLowerInvariant(), asset.Sha256);
        Assert.Matches("^[a-f0-9]{32}\\.png$", asset.StoredFileName);
        Assert.Equal($"Assets/{asset.StoredFileName}", asset.RelativePath);
        Assert.DoesNotContain("My Logo", asset.StoredFileName, StringComparison.Ordinal);
        Assert.Same(asset, Assert.Single(project.Assets));
        Assert.True(service.TryGetFullPath(_projectDirectory, asset, out var storedPath));
        Assert.Equal(ValidPng, await File.ReadAllBytesAsync(storedPath));
    }

    [Fact]
    public async Task Import_SameSourceTwice_NeverOverwritesExistingAsset()
    {
        var source = WriteSource("logo.png", ValidPng);
        var project = Project.CreateDefault();
        var service = CreateService();

        var first = await service.ImportAsync(project, _projectDirectory, source);
        var second = await service.ImportAsync(project, _projectDirectory, source);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Asset!.StoredFileName, second.Asset!.StoredFileName);
        Assert.Equal(2, project.Assets.Count);
    }

    [Fact]
    public async Task Import_DisguisedExecutable_IsRejectedBySignature()
    {
        var source = WriteSource("not-really.png", "MZ fake executable"u8.ToArray());
        var project = Project.CreateDefault();

        var result = await CreateService().ImportAsync(project, _projectDirectory, source);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetImportFailure.InvalidContent, result.Failure);
        Assert.Empty(project.Assets);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_projectDirectory, AssetService.AssetsFolderName)));
    }

    [Fact]
    public async Task Import_UnsupportedExtension_IsRejectedBeforeStorage()
    {
        var source = WriteSource("payload.exe", "MZ"u8.ToArray());
        var project = Project.CreateDefault();

        var result = await CreateService().ImportAsync(project, _projectDirectory, source);

        Assert.Equal(AssetImportFailure.UnsupportedType, result.Failure);
        Assert.Empty(project.Assets);
        Assert.False(Directory.Exists(Path.Combine(_projectDirectory, AssetService.AssetsFolderName)));
    }

    [Fact]
    public async Task Import_OverConfiguredLimit_IsRejected()
    {
        var source = WriteSource("large.png", ValidPng);
        var project = Project.CreateDefault();
        var service = new AssetService(new AssetImportOptions { MaxImageBytes = 8 });

        var result = await service.ImportAsync(project, _projectDirectory, source);

        Assert.Equal(AssetImportFailure.FileTooLarge, result.Failure);
        Assert.Empty(project.Assets);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"https://evil.example/a.png\"/></svg>")]
    [InlineData("<!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><svg>&xxe;</svg>")]
    public async Task Import_ActiveOrExternallyReferencingSvg_IsRejected(string svg)
    {
        var source = WriteSource("unsafe.svg", Encoding.UTF8.GetBytes(svg));
        var project = Project.CreateDefault();

        var result = await CreateService().ImportAsync(project, _projectDirectory, source);

        Assert.Equal(AssetImportFailure.InvalidContent, result.Failure);
        Assert.Empty(project.Assets);
    }

    [Fact]
    public async Task Import_SafeSvg_IsAccepted()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><path d=\"M0 0h10v10z\"/></svg>";
        var source = WriteSource("shape.svg", Encoding.UTF8.GetBytes(svg));
        var project = Project.CreateDefault();

        var result = await CreateService().ImportAsync(project, _projectDirectory, source);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetKinds.Svg, result.Asset!.Kind);
    }

    [Fact]
    public void TryGetFullPath_RejectsTraversalMetadata()
    {
        var asset = new ProjectAsset
        {
            StoredFileName = "..\\outside.txt",
            RelativePath = "Assets/../outside.txt",
        };

        var resolved = CreateService().TryGetFullPath(_projectDirectory, asset, out var fullPath);

        Assert.False(resolved);
        Assert.Empty(fullPath);
    }

    [Fact]
    public async Task Delete_RemovesOnlyValidatedManagedFileAndMetadata()
    {
        var source = WriteSource("logo.png", ValidPng);
        var project = Project.CreateDefault();
        var service = CreateService();
        var import = await service.ImportAsync(project, _projectDirectory, source);
        Assert.True(service.TryGetFullPath(_projectDirectory, import.Asset!, out var storedPath));

        var deletion = await service.DeleteAsync(project, _projectDirectory, import.Asset!);

        Assert.True(deletion.IsSuccess);
        Assert.False(File.Exists(storedPath));
        Assert.Empty(project.Assets);
        Assert.True(File.Exists(source));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private AssetService CreateService() => new(new AssetImportOptions());

    private string WriteSource(string fileName, byte[] contents)
    {
        var path = Path.Combine(_sourceDirectory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }
}
