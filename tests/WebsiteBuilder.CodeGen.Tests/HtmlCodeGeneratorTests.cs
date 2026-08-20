using WebsiteBuilder.CodeGen;
using WebsiteBuilder.Core.Models;
using Xunit;

namespace WebsiteBuilder.CodeGen.Tests;

/// <summary>
/// Covers the static-HTML generator (Phase 11a): semantic markup, per-element CSS
/// with geometry, project variables, and deterministic output.
/// </summary>
public class HtmlCodeGeneratorTests
{
    private static Project BuildProject()
    {
        var project = Project.CreateDefault("Demo Site");
        project.Variables["brand"] = "#2563eb";

        var root = project.Pages[0].Root;
        root.Children.Add(new ElementNode
        {
            Id = "hero-heading",
            Type = ElementTypes.Heading,
            X = 40,
            Y = 24,
            Width = 600,
            Height = 60,
            Text = "Hello & welcome",
            Styles = { ["color"] = "var(--brand)", ["font-size"] = "32px" },
        });
        root.Children.Add(new ElementNode
        {
            Id = "cta",
            Type = ElementTypes.Button,
            X = 40,
            Y = 120,
            Width = 160,
            Height = 44,
            Text = "Sign up",
            Rotation = 15,
        });
        return project;
    }

    [Fact]
    public void Generate_ProducesHtmlCssAndJsFiles()
    {
        var files = new HtmlCodeGenerator().Generate(BuildProject());

        Assert.Equal(3, files.Count);
        Assert.Contains(files, f => f.RelativePath == "index.html");
        Assert.Contains(files, f => f.RelativePath == "css/styles.css");
        Assert.Contains(files, f => f.RelativePath == "js/main.js");
    }

    [Fact]
    public void Html_LinksDeferredScript()
    {
        var html = new HtmlCodeGenerator().Generate(BuildProject())
            .Single(f => f.RelativePath == "index.html").Contents;

        Assert.Contains("<script src=\"js/main.js\" defer></script>", html);
    }

    [Fact]
    public void Css_EmitsResponsiveMediaQueries_WidestToNarrowest()
    {
        var project = Project.CreateDefault("Responsive");
        project.Pages[0].Root.Children.Add(new ElementNode
        {
            Id = "title",
            Type = ElementTypes.Heading,
            X = 0,
            Y = 0,
            Width = 400,
            Height = 50,
            Styles = { ["font-size"] = "48px" },
            ResponsiveStyles =
            {
                ["laptop"] = new Dictionary<string, string> { ["font-size"] = "36px" },
                ["mobile"] = new Dictionary<string, string> { ["font-size"] = "24px" },
            },
        });

        var css = new HtmlCodeGenerator().Generate(project)
            .Single(f => f.RelativePath == "css/styles.css").Contents;

        var laptopAt = css.IndexOf("@media (max-width: 1280px)", StringComparison.Ordinal);
        var mobileAt = css.IndexOf("@media (max-width: 480px)", StringComparison.Ordinal);

        Assert.True(laptopAt >= 0, "laptop media query missing");
        Assert.True(mobileAt >= 0, "mobile media query missing");
        Assert.True(laptopAt < mobileAt, "wider breakpoint must come before narrower");
        Assert.Contains(".wb-title {", css);
        Assert.Contains("font-size: 36px;", css); // laptop override
        Assert.Contains("font-size: 24px;", css); // mobile override
    }

    [Fact]
    public void Html_UsesSemanticTags_EscapesText_AndLinksStylesheet()
    {
        var html = new HtmlCodeGenerator().Generate(BuildProject())
            .Single(f => f.RelativePath == "index.html").Contents;

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<title>Home — Demo Site</title>", html);
        Assert.Contains("<link rel=\"stylesheet\" href=\"css/styles.css\">", html);
        Assert.Contains("<section class=\"wb-page\">", html); // root
        // Heading has styles → geometry class + a shared appearance class; the
        // button has no styles → geometry class only.
        Assert.Contains("<h2 class=\"wb-hero-heading s1\">Hello &amp; welcome</h2>", html);
        Assert.Contains("<button class=\"wb-cta\">Sign up</button>", html);
    }

    [Fact]
    public void Css_EmitsVariables_AndPerElementGeometry()
    {
        var css = new HtmlCodeGenerator().Generate(BuildProject())
            .Single(f => f.RelativePath == "css/styles.css").Contents;

        Assert.Contains("--brand: #2563eb;", css);
        Assert.Contains(".wb-page {", css);
        Assert.Contains("max-width: 1440px;", css);

        Assert.Contains(".wb-hero-heading {", css);
        Assert.Contains("position: absolute;", css);
        Assert.Contains("left: 40px;", css);
        Assert.Contains("top: 24px;", css);
        // Appearance lives in a shared class, not the per-element geometry rule.
        Assert.Contains(".s1 {", css);
        Assert.Contains("color: var(--brand);", css);

        Assert.Contains("transform: rotate(15deg);", css); // button rotation
    }

    [Fact]
    public void Css_DedupesIdenticalAppearanceIntoOneSharedClass()
    {
        var project = Project.CreateDefault("Dedupe");
        var root = project.Pages[0].Root;
        var shared = new Dictionary<string, string> { ["background"] = "#111", ["color"] = "#fff" };
        root.Children.Add(new ElementNode { Id = "a", Type = ElementTypes.Button, Styles = new(shared) });
        root.Children.Add(new ElementNode { Id = "b", Type = ElementTypes.Button, Styles = new(shared) });

        var files = new HtmlCodeGenerator().Generate(project);
        var html = files.Single(f => f.RelativePath == "index.html").Contents;
        var css = files.Single(f => f.RelativePath == "css/styles.css").Contents;

        // Both elements reference the same shared appearance class…
        Assert.Contains("class=\"wb-a s1\"", html);
        Assert.Contains("class=\"wb-b s1\"", html);
        // …and there is no second appearance class for the duplicate.
        Assert.DoesNotContain(".s2 {", css);
        // The shared declarations are emitted once.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(css, @"\.s1 \{"));
    }

    [Fact]
    public void Generate_MultiPage_EmitsOneHtmlPerPage_SharingCssAndJs()
    {
        var project = Project.CreateDefault("Multi");
        project.Pages.Add(new Page { Id = "p2", Name = "About", Route = "about" });
        project.Pages[1].Root.Children.Add(new ElementNode
        {
            Id = "about-title", Type = ElementTypes.Heading, X = 10, Y = 10, Width = 200, Height = 40, Text = "About",
        });

        var files = new HtmlCodeGenerator().Generate(project);

        // index.html + about.html + one shared css + one shared js.
        Assert.Contains(files, f => f.RelativePath == "index.html");
        Assert.Contains(files, f => f.RelativePath == "about.html");
        Assert.Single(files, f => f.RelativePath == "css/styles.css");
        Assert.Single(files, f => f.RelativePath == "js/main.js");

        var about = files.Single(f => f.RelativePath == "about.html").Contents;
        Assert.Contains("<title>About — Multi</title>", about);
        Assert.Contains("<link rel=\"stylesheet\" href=\"css/styles.css\">", about);

        // The second page's element geometry lands in the shared stylesheet.
        var css = files.Single(f => f.RelativePath == "css/styles.css").Contents;
        Assert.Contains(".wb-about-title {", css);
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var generator = new HtmlCodeGenerator();
        var project = BuildProject();

        var first = generator.Generate(project);
        var second = generator.Generate(project);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].RelativePath, second[i].RelativePath);
            Assert.Equal(first[i].Contents, second[i].Contents);
        }
    }
}
