using WebsiteBuilder.Core.Components;
using WebsiteBuilder.Core.Models;
using Xunit;

namespace WebsiteBuilder.CodeGen.Tests;

public sealed class ComponentExportTests
{
    [Fact]
    public void EveryBuiltInComponent_ExportsToStaticAndNextJs()
    {
        foreach (var definition in BuiltInComponentLibrary.All)
        {
            var project = Project.CreateDefault(definition.Name);
            project.Pages[0].Root.Children.Add(definition.Root);

            var html = new HtmlCodeGenerator().Generate(project);
            var react = new ReactCodeGenerator().Generate(project);

            Assert.Contains(html, file => file.RelativePath == "index.html");
            Assert.Contains(react, file => file.RelativePath == "app/page.tsx");
        }
    }

    [Fact]
    public void ContactForm_ExportsSemanticControls()
    {
        var project = Project.CreateDefault("Contact");
        project.Pages[0].Root.Children.Add(
            BuiltInComponentLibrary.All.Single(definition => definition.Id == "form-contact").Root);

        var html = new HtmlCodeGenerator().Generate(project)
            .Single(file => file.RelativePath == "index.html").Contents;
        Assert.Contains("<form", html);
        Assert.Contains("<input", html);
        Assert.Contains("<textarea", html);
        Assert.Contains("type=\"submit\"", html);
    }
}
