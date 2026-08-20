using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Serialization;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

/// <summary>
/// Smoke tests confirming the canonical Project JSON model serializes and
/// round-trips. This also exercises the shared <see cref="ProjectSerializer"/>
/// options that the whole app relies on.
/// </summary>
public class ProjectSerializerTests
{
    [Fact]
    public void CreateDefault_HasSingleHomePageAndDefaultBreakpoints()
    {
        var project = Project.CreateDefault("My Site");

        Assert.Equal("My Site", project.Name);
        Assert.Single(project.Pages);
        Assert.Equal("Home", project.Pages[0].Name);
        Assert.Empty(project.Pages[0].Root.Children);
        Assert.Equal(Breakpoint.Defaults.Count, project.Breakpoints.Count);
        Assert.Contains(project.Breakpoints, b => b.IsBase);
    }

    [Fact]
    public void Serialize_Then_Deserialize_PreservesModel()
    {
        var project = Project.CreateDefault("Round Trip");
        project.Pages[0].Root.Children.Add(new ElementNode
        {
            Id = "button001",
            Type = ElementTypes.Button,
            X = 150,
            Y = 300,
            Width = 180,
            Height = 45,
            Text = "Login",
            Styles =
            {
                ["background"] = "#2563eb",
                ["color"] = "#ffffff",
            },
        });
        project.Assets.Add(new ProjectAsset
        {
            Id = "asset001",
            Name = "Logo.png",
            StoredFileName = "abc123.png",
            RelativePath = "Assets/abc123.png",
            Kind = AssetKinds.Image,
            MediaType = "image/png",
            SizeBytes = 42,
            Sha256 = new string('a', 64),
        });

        var json = ProjectSerializer.Serialize(project);
        var restored = ProjectSerializer.Deserialize(json);

        Assert.Equal(project.Name, restored.Name);
        var button = Assert.Single(restored.Pages[0].Root.Children);
        Assert.Equal("button001", button.Id);
        Assert.Equal(ElementTypes.Button, button.Type);
        Assert.Equal("Login", button.Text);
        Assert.Equal("#2563eb", button.Styles["background"]);
        Assert.Equal(45, button.Height);
        var asset = Assert.Single(restored.Assets);
        Assert.Equal("asset001", asset.Id);
        Assert.Equal("Assets/abc123.png", asset.RelativePath);
        Assert.Equal("image/png", asset.MediaType);
    }

    [Fact]
    public void DescendantsAndSelf_WalksFullTree()
    {
        var root = new ElementNode { Id = "root" };
        var child = new ElementNode { Id = "child" };
        var grandchild = new ElementNode { Id = "grandchild" };
        child.Children.Add(grandchild);
        root.Children.Add(child);

        var ids = root.DescendantsAndSelf().Select(n => n.Id).ToArray();

        Assert.Equal(new[] { "root", "child", "grandchild" }, ids);
    }
}
