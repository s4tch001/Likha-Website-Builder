using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

public class ProjectServiceTests
{
    [Fact]
    public void New_SetsCurrentAndRaisesEvent()
    {
        var service = new ProjectService();
        Project? raised = null;
        service.CurrentChanged += (_, p) => raised = p;

        var project = service.New("Demo");

        Assert.Same(project, service.Current);
        Assert.Same(project, raised);
        Assert.Null(service.CurrentPath);
        Assert.Equal("Demo", project.Name);
    }

    [Fact]
    public async Task SaveAs_Then_Open_RoundTripsThroughDisk()
    {
        var service = new ProjectService();
        var project = service.New("Disk Test");
        project.Pages[0].Root.Children.Add(new ElementNode { Id = "n1", Type = ElementTypes.Button, Text = "Hi" });

        var path = Path.Combine(Path.GetTempPath(), $"wb_{Guid.NewGuid():N}{ProjectService.FileExtension}");
        try
        {
            await service.SaveAsAsync(path);
            Assert.Equal(path, service.CurrentPath);
            Assert.True(File.Exists(path));

            var reopened = new ProjectService();
            var loaded = await reopened.OpenAsync(path);

            Assert.Equal("Disk Test", loaded.Name);
            var node = Assert.Single(loaded.Pages[0].Root.Children);
            Assert.Equal("n1", node.Id);
            Assert.Equal("Hi", node.Text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Save_WithoutPath_Throws()
    {
        var service = new ProjectService();
        service.New();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync());
    }

    [Fact]
    public void New_IsNotDirty_And_ApplyEditorUpdate_MarksDirty()
    {
        var service = new ProjectService();
        var raised = 0;
        service.DirtyChanged += (_, _) => raised++;

        var project = service.New();
        Assert.False(service.IsDirty);

        service.ApplyEditorUpdate(project);
        Assert.True(service.IsDirty);
        Assert.True(raised >= 1);

        // Opening/new again resets the dirty flag.
        service.New();
        Assert.False(service.IsDirty);
    }

    [Fact]
    public void TryApplyEditorUpdate_RejectsStaleSnapshot()
    {
        var service = new ProjectService();
        var project = service.New("Revisioned");
        var initialRevision = service.Revision;

        service.ApplyHostUpdate(project);
        var authoritativeRevision = service.Revision;

        var accepted = service.TryApplyEditorUpdate(
            Project.CreateDefault("Stale"),
            initialRevision,
            out var returnedRevision);

        Assert.False(accepted);
        Assert.Equal(authoritativeRevision, returnedRevision);
        Assert.Same(project, service.Current);
        Assert.Equal("Revisioned", service.Current!.Name);
    }

    [Fact]
    public void ApplyHostUpdate_RaisesHostMutationAndAdvancesRevision()
    {
        var service = new ProjectService();
        var project = service.New();
        var before = service.Revision;
        Project? published = null;
        service.HostMutated += (_, value) => published = value;

        service.ApplyHostUpdate(project);

        Assert.Equal(before + 1, service.Revision);
        Assert.Same(project, published);
        Assert.True(service.IsDirty);
    }

    [Fact]
    public async Task Save_ClearsDirtyFlag()
    {
        var service = new ProjectService();
        var project = service.New("Dirty");
        service.ApplyEditorUpdate(project);
        Assert.True(service.IsDirty);

        var folder = Path.Combine(Path.GetTempPath(), $"wb_{Guid.NewGuid():N}");
        try
        {
            await service.SaveToFolderAsync(folder);
            Assert.False(service.IsDirty);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveToFolder_ScaffoldsLayout_And_OpensFromFolder()
    {
        var service = new ProjectService();
        var project = service.New("Folder Project");
        project.Pages[0].Root.Children.Add(new ElementNode { Id = "n1", Type = ElementTypes.Button });

        var folder = Path.Combine(Path.GetTempPath(), $"wb_{Guid.NewGuid():N}");
        try
        {
            await service.SaveToFolderAsync(folder);

            // project.json written + standard subfolders scaffolded.
            Assert.True(File.Exists(Path.Combine(folder, ProjectService.ProjectFileName)));
            Assert.Equal(folder, service.ProjectDirectory);
            foreach (var sub in ProjectService.StandardFolders)
            {
                Assert.True(Directory.Exists(Path.Combine(folder, sub)), $"missing folder: {sub}");
            }

            // Opening by folder path resolves project.json.
            var reopened = new ProjectService();
            var loaded = await reopened.OpenAsync(folder);
            Assert.Equal("Folder Project", loaded.Name);
            Assert.Equal("n1", Assert.Single(loaded.Pages[0].Root.Children).Id);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImportedAssetMetadata_RoundTripsWithFolderProject()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wb_{Guid.NewGuid():N}");
        var source = Path.Combine(Path.GetTempPath(), $"wb_source_{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(
                source,
                [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01]);

            var projects = new ProjectService();
            var project = projects.New("Asset Project");
            await projects.SaveToFolderAsync(folder);

            var assets = new AssetService(new AssetImportOptions());
            var imported = await assets.ImportAsync(project, folder, source);
            Assert.True(imported.IsSuccess);
            projects.ApplyEditorUpdate(project);
            await projects.SaveAsync();

            var reopened = new ProjectService();
            var loaded = await reopened.OpenAsync(folder);
            var restored = Assert.Single(loaded.Assets);
            Assert.Equal(imported.Asset!.Id, restored.Id);
            Assert.True(assets.TryGetFullPath(folder, restored, out var fullPath));
            Assert.True(File.Exists(fullPath));
        }
        finally
        {
            if (File.Exists(source))
            {
                File.Delete(source);
            }

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
