using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.CodeGen;

/// <summary>A single generated file: a relative path plus its text contents.</summary>
/// <param name="RelativePath">Path relative to the export root (e.g. "index.html", "css/styles.css").</param>
/// <param name="Contents">UTF-8 text contents of the file.</param>
public readonly record struct GeneratedFile(string RelativePath, string Contents);

/// <summary>The output target a generator produces.</summary>
public enum CodeGenTarget
{
    /// <summary>Static HTML5 + CSS3 + vanilla JavaScript.</summary>
    StaticHtml,

    /// <summary>A Next.js App Router + React + TypeScript project ready for <c>npm install</c>.</summary>
    React,
}

/// <summary>
/// Transforms a <see cref="Project"/> into a set of source files for a given
/// target. Implementations are pure functions of their input (no file IO), which
/// keeps them deterministic and unit-testable; writing the files to disk is the
/// caller's responsibility. Concrete generators are added in Phases 11 and 12.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>The target this generator emits.</summary>
    CodeGenTarget Target { get; }

    /// <summary>Produces the full file set for the project.</summary>
    IReadOnlyList<GeneratedFile> Generate(Project project);
}
