using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Services;

/// <summary>Validates and stores project assets beneath the managed Assets directory.</summary>
public interface IAssetService
{
    /// <summary>
    /// Imports one untrusted local file, adding metadata to <paramref name="project"/>
    /// only after the validated bytes have been committed successfully.
    /// </summary>
    Task<AssetImportResult> ImportAsync(
        Project project,
        string projectDirectory,
        string sourcePath,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves managed metadata to a contained absolute path.</summary>
    bool TryGetFullPath(string projectDirectory, ProjectAsset asset, out string fullPath);

    /// <summary>Deletes a managed asset and removes its metadata.</summary>
    Task<AssetDeleteResult> DeleteAsync(
        Project project,
        string projectDirectory,
        ProjectAsset asset,
        CancellationToken cancellationToken = default);
}

public enum AssetImportFailure
{
    None,
    InvalidPath,
    FileNotFound,
    UnsupportedType,
    InvalidContent,
    FileTooLarge,
    UnsafeFile,
    StorageError,
}

/// <summary>Structured import outcome suitable for UI status without exception parsing.</summary>
public readonly record struct AssetImportResult(
    ProjectAsset? Asset,
    AssetImportFailure Failure,
    string Message)
{
    public bool IsSuccess => Asset is not null && Failure == AssetImportFailure.None;

    public static AssetImportResult Success(ProjectAsset asset) =>
        new(asset, AssetImportFailure.None, string.Empty);

    public static AssetImportResult Fail(AssetImportFailure failure, string message) =>
        new(null, failure, message);
}

/// <summary>Structured deletion outcome for a managed asset.</summary>
public readonly record struct AssetDeleteResult(bool IsSuccess, string Message)
{
    public static AssetDeleteResult Success() => new(true, string.Empty);

    public static AssetDeleteResult Fail(string message) => new(false, message);
}
