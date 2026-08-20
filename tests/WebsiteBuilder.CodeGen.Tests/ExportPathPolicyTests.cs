using WebsiteBuilder.CodeGen;
using Xunit;

namespace WebsiteBuilder.CodeGen.Tests;

public sealed class ExportPathPolicyTests
{
    [Fact]
    public void ResolveContainedPath_AllowsNestedRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "likha-export");

        var result = ExportPathPolicy.ResolveContainedPath(root, "app/about/page.tsx");

        Assert.StartsWith(Path.GetFullPath(root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("app", "about", "page.tsx"), result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("app/../../../outside.html")]
    public void ResolveContainedPath_RejectsTraversal(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "likha-export");

        Assert.Throws<InvalidDataException>(() => ExportPathPolicy.ResolveContainedPath(root, relativePath));
    }
}
