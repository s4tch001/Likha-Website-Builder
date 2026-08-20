using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Serialization;
using WebsiteBuilder.Core.Validation;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

public sealed class ProjectValidatorTests
{
    [Fact]
    public void ValidateAndThrow_AcceptsDefaultProject()
    {
        ProjectValidator.ValidateAndThrow(Project.CreateDefault());
    }

    [Fact]
    public void ValidateAndThrow_RejectsDuplicateElementIds()
    {
        var project = Project.CreateDefault();
        project.Pages[0].Root.Children.Add(new ElementNode { Id = "duplicate" });
        project.Pages[0].Root.Children.Add(new ElementNode { Id = "duplicate" });

        Assert.Throws<ProjectValidationException>(() => ProjectValidator.ValidateAndThrow(project));
    }

    [Fact]
    public void ValidateAndThrow_RejectsRouteTraversal()
    {
        var project = Project.CreateDefault();
        project.Pages[0].Route = "../../outside";

        Assert.Throws<ProjectValidationException>(() => ProjectValidator.ValidateAndThrow(project));
    }

    [Fact]
    public void ValidateAndThrow_RejectsNonFiniteGeometry()
    {
        var project = Project.CreateDefault();
        project.Pages[0].Root.X = double.NaN;

        Assert.Throws<ProjectValidationException>(() => ProjectValidator.ValidateAndThrow(project));
    }

    [Fact]
    public void ValidateAndThrow_RejectsUnknownResponsiveBreakpoint()
    {
        var project = Project.CreateDefault();
        project.Pages[0].Root.ResponsiveStyles["ghost"] = new() { ["color"] = "red" };

        Assert.Throws<ProjectValidationException>(() => ProjectValidator.ValidateAndThrow(project));
    }

    [Theory]
    [InlineData("onclick", "alert(1)")]
    [InlineData("href", "javascript:alert(1)")]
    [InlineData("srcdoc", "<script>alert(1)</script>")]
    public void ValidateAndThrow_RejectsDangerousAttributes(string name, string value)
    {
        var project = Project.CreateDefault();
        project.Pages[0].Root.Attributes[name] = value;

        Assert.Throws<ProjectValidationException>(() => ProjectValidator.ValidateAndThrow(project));
    }

    [Fact]
    public void Deserialize_RejectsExplicitNullCollections()
    {
        var json = ProjectSerializer.Serialize(Project.CreateDefault())
            .Replace("\"pages\": [", "\"pages\": null, \"ignoredPages\": [", StringComparison.Ordinal);

        Assert.Throws<ProjectValidationException>(() => ProjectSerializer.Deserialize(json));
    }
}
