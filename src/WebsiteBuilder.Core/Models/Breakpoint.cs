namespace WebsiteBuilder.Core.Models;

/// <summary>
/// A responsive breakpoint. Styles cascade from the widest (Base/Desktop)
/// breakpoint down, mirroring a mobile-last authoring model where each
/// narrower breakpoint only stores the properties that differ.
/// </summary>
public sealed class Breakpoint
{
    /// <summary>Stable identifier used as the key for per-breakpoint overrides (e.g. "desktop").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the responsive toolbar (e.g. "Desktop").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Maximum viewport width in pixels this breakpoint targets. 0 means "base / no upper bound".</summary>
    public int MaxWidth { get; set; }

    /// <summary>Whether this is the base breakpoint whose styles all others inherit from.</summary>
    public bool IsBase { get; set; }

    /// <summary>The standard breakpoint set shipped with every new project.</summary>
    public static IReadOnlyList<Breakpoint> Defaults { get; } = new[]
    {
        new Breakpoint { Id = "desktop", Label = "Desktop", MaxWidth = 0, IsBase = true },
        new Breakpoint { Id = "laptop", Label = "Laptop", MaxWidth = 1280 },
        new Breakpoint { Id = "tablet", Label = "Tablet", MaxWidth = 768 },
        new Breakpoint { Id = "mobile", Label = "Mobile", MaxWidth = 480 },
    };
}
