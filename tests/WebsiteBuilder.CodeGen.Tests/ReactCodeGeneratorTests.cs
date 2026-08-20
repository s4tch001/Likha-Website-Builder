using WebsiteBuilder.CodeGen;
using WebsiteBuilder.Core.Models;
using Xunit;

namespace WebsiteBuilder.CodeGen.Tests;

/// <summary>Covers the Next.js/React/TypeScript project exporter.</summary>
public class ReactCodeGeneratorTests
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
            Attributes = { ["type"] = "button" },
        });
        return project;
    }

    [Fact]
    public void Generate_ProducesNextAppRouterScaffold()
    {
        var generator = new ReactCodeGenerator();
        var files = generator.Generate(BuildProject());

        Assert.Equal(CodeGenTarget.React, generator.Target);
        foreach (var expected in new[]
                 {
                     "package.json", "next.config.ts", "tsconfig.json", ".gitignore", "README.md",
                     "public/_headers", "app/globals.css", "app/layout.tsx", "app/error.tsx",
                     "app/not-found.tsx", "app/page.tsx",
                 })
        {
            Assert.Contains(files, file => file.RelativePath == expected);
        }

        Assert.DoesNotContain(files, file => file.RelativePath.Contains("vite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PackageJson_PinsCompatibleNextReactAndNativeTypeScript()
    {
        var packageJson = FileContents(BuildProject(), "package.json");

        Assert.Contains($"\"next\": \"{ReactCodeGenerator.NextVersion}\"", packageJson);
        Assert.Contains($"\"react\": \"{ReactCodeGenerator.ReactVersion}\"", packageJson);
        Assert.Contains($"\"react-dom\": \"{ReactCodeGenerator.ReactVersion}\"", packageJson);
        Assert.Contains(
            $"\"@typescript/native\": \"npm:typescript@{ReactCodeGenerator.TypeScriptVersion}\"",
            packageJson);
        Assert.Contains(
            $"\"typescript\": \"npm:@typescript/typescript6@{ReactCodeGenerator.TypeScriptApiVersion}\"",
            packageJson);
        Assert.Contains("\"typecheck\": \"next typegen && tsc --noEmit\"", packageJson);
        Assert.Contains("\"node\": \">=20.9.0\"", packageJson);
        Assert.DoesNotContain("vite", packageJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("react-router", packageJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NextConfig_UsesStaticExportAndTypeScriptSevenCli()
    {
        var config = FileContents(BuildProject(), "next.config.ts");

        Assert.Contains("output: \"export\"", config);
        Assert.Contains("useTypeScriptCli: true", config);
        Assert.Contains("unoptimized: true", config);
        Assert.Contains("satisfies NextConfig", config);
    }

    [Fact]
    public void TsConfig_EnablesStrictNativeCompatibleChecks()
    {
        var config = FileContents(BuildProject(), "tsconfig.json");

        Assert.Contains("\"strict\": true", config);
        Assert.Contains("\"noUncheckedIndexedAccess\": true", config);
        Assert.Contains("\"exactOptionalPropertyTypes\": true", config);
        Assert.Contains("\"jsx\": \"react-jsx\"", config);
        Assert.Contains("\"name\": \"next\"", config);
    }

    [Fact]
    public void RootLayout_UsesMetadataApiAndGlobalCss()
    {
        var layout = FileContents(BuildProject(), "app/layout.tsx");

        Assert.Contains("import type { Metadata } from \"next\";", layout);
        Assert.Contains("import \"./globals.css\";", layout);
        Assert.Contains("default: \"Demo Site\"", layout);
        Assert.Contains("satisfies Metadata", layout);
        Assert.Contains("export default function RootLayout", layout);
    }

    [Fact]
    public void Page_RendersServerComponentWithSafeTextAndSharedClasses()
    {
        var home = FileContents(BuildProject(), "app/page.tsx");

        Assert.Contains("export default function HomePage()", home);
        Assert.Contains("<section className=\"wb-page\">", home);
        Assert.Contains("<h2 className=\"wb-hero-heading s1\">{\"Hello \\u0026 welcome\"}</h2>", home);
        Assert.Contains("<button className=\"wb-cta\" type=\"button\">{\"Sign up\"}</button>", home);
        Assert.DoesNotContain("\"use client\"", home);
    }

    [Fact]
    public void MultiPage_EmitsOneAppRouterRoutePerPage()
    {
        var project = BuildProject();
        project.Pages.Add(new Page { Id = "p2", Name = "About Us", Route = "company/about" });

        var files = new ReactCodeGenerator().Generate(project);
        var about = files.Single(file => file.RelativePath == "app/company/about/page.tsx").Contents;

        Assert.Contains(files, file => file.RelativePath == "app/page.tsx");
        Assert.Contains("export default function AboutUsPage()", about);
        Assert.Contains("title: \"About Us\"", about);
    }

    [Fact]
    public void UnsafeRouteAndAttributes_AreRejectedAtTheBoundary()
    {
        var project = BuildProject();
        var unsafePage = new Page { Id = "unsafe", Name = "Unsafe", Route = "../../Admin Panel" };
        unsafePage.Root.Children.Add(new ElementNode
        {
            Id = "unsafe-link",
            Type = ElementTypes.Link,
            Text = "Open",
            Attributes =
            {
                ["href"] = "javascript:alert(1)",
                ["onClick"] = "alert(1)",
                ["dangerouslySetInnerHTML"] = "<img src=x onerror=alert(1)>",
                ["data-testid"] = "safe-link",
            },
        });
        project.Pages.Add(unsafePage);

        Assert.Throws<WebsiteBuilder.Core.Validation.ProjectValidationException>(
            () => new ReactCodeGenerator().Generate(project));
    }

    [Fact]
    public void ImageElement_RejectsScriptUrl()
    {
        var project = BuildProject();
        project.Pages[0].Root.Children.Add(new ElementNode
        {
            Id = "cover",
            Name = "Cover image",
            Type = ElementTypes.Image,
            Width = 320,
            Height = 180,
            Attributes = { ["src"] = "javascript:alert(1)", ["alt"] = "Cover" },
        });

        Assert.Throws<WebsiteBuilder.Core.Validation.ProjectValidationException>(
            () => FileContents(project, "app/page.tsx"));
    }

    [Fact]
    public void CssAndSecurityFiles_AreIncluded()
    {
        var project = BuildProject();
        var css = FileContents(project, "app/globals.css");
        var headers = FileContents(project, "public/_headers");

        Assert.Equal(WebStyleSheet.Build(project).Css, css);
        Assert.Contains(".wb-page {", css);
        Assert.Contains("Content-Security-Policy:", headers);
        Assert.Contains("frame-ancestors 'none'", headers);
        Assert.Contains("X-Content-Type-Options: nosniff", headers);
    }

    [Fact]
    public void ManagedAssets_UsePublicUrlsAndFontFace()
    {
        var project = BuildProject();
        project.Assets.Add(new ProjectAsset
        {
            Id = "font123",
            Name = "Site font",
            StoredFileName = "site.woff2",
            RelativePath = "Assets/site.woff2",
            Kind = AssetKinds.Font,
            MediaType = "font/woff2",
            SizeBytes = 1,
            Sha256 = new string('a', 64),
        });
        project.Assets.Add(new ProjectAsset
        {
            Id = "track",
            Name = "track.mp3",
            StoredFileName = "track.mp3",
            RelativePath = "Assets/track.mp3",
            Kind = AssetKinds.Audio,
            MediaType = "audio/mpeg",
            SizeBytes = 1,
            Sha256 = new string('b', 64),
        });
        project.Assets.Add(new ProjectAsset
        {
            Id = "manual",
            Name = "manual.pdf",
            StoredFileName = "manual.pdf",
            RelativePath = "Assets/manual.pdf",
            Kind = AssetKinds.Document,
            MediaType = "application/pdf",
            SizeBytes = 1,
            Sha256 = new string('c', 64),
        });
        project.Pages[0].Root.Children.Add(new ElementNode
        {
            Id = "track",
            Type = ElementTypes.Audio,
            Attributes = { ["src"] = "Assets/track.mp3", ["controls"] = "true" },
        });
        project.Pages[0].Root.Children.Add(new ElementNode
        {
            Id = "manual",
            Type = ElementTypes.Link,
            Text = "Manual",
            Attributes = { ["href"] = "Assets/manual.pdf", ["download"] = "manual.pdf" },
        });

        var page = FileContents(project, "app/page.tsx");
        var css = FileContents(project, "app/globals.css");
        Assert.Contains("src=\"/Assets/track.mp3\"", page);
        Assert.Contains("href=\"/Assets/manual.pdf\"", page);
        Assert.Contains("url('/Assets/site.woff2')", css);
        Assert.Contains("font-family: 'LikhaAsset_font123'", css);
    }

    private static string FileContents(Project project, string relativePath) =>
        new ReactCodeGenerator().Generate(project)
            .Single(file => file.RelativePath == relativePath)
            .Contents;
}
