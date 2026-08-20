using WebsiteBuilder.CodeGen;
using WebsiteBuilder.Core.Models;
using Xunit;

namespace WebsiteBuilder.CodeGen.Tests;

/// <summary>
/// Smoke tests for the code-generation seam. Concrete HTML/React emitters arrive
/// in Phases 11-12; here we confirm the <see cref="ICodeGenerator"/> contract and
/// <see cref="GeneratedFile"/> value type are usable by a generator implementation.
/// </summary>
public class CodeGenContractTests
{
    /// <summary>Minimal in-test generator used to validate the abstraction shape.</summary>
    private sealed class StubHtmlGenerator : ICodeGenerator
    {
        public CodeGenTarget Target => CodeGenTarget.StaticHtml;

        public IReadOnlyList<GeneratedFile> Generate(Project project)
        {
            return new[]
            {
                new GeneratedFile("index.html", $"<!-- {project.Name} -->"),
            };
        }
    }

    [Fact]
    public void Generator_ProducesFilesForProject()
    {
        ICodeGenerator generator = new StubHtmlGenerator();
        var project = Project.CreateDefault("Contract Test");

        var files = generator.Generate(project);

        Assert.Equal(CodeGenTarget.StaticHtml, generator.Target);
        var file = Assert.Single(files);
        Assert.Equal("index.html", file.RelativePath);
        Assert.Contains("Contract Test", file.Contents);
    }

    [Fact]
    public void GeneratedFile_EqualityIsValueBased()
    {
        var a = new GeneratedFile("a.css", ".x{}");
        var b = new GeneratedFile("a.css", ".x{}");

        Assert.Equal(a, b);
    }
}
