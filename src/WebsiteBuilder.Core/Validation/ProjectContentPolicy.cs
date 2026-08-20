using System.Text.RegularExpressions;

namespace WebsiteBuilder.Core.Validation;

/// <summary>Shared allowlist policy for routes, CSS declarations, HTML attributes, and URLs.</summary>
public static partial class ProjectContentPolicy
{
    private static readonly string[] UrlAttributes = ["href", "src", "action", "poster", "cite"];

    /// <summary>Returns true for a canonical relative site route.</summary>
    public static bool IsSafeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route) || route.Length > 200 || route.Contains('\\'))
        {
            return false;
        }

        var trimmed = route.Trim('/');
        return trimmed.Length > 0
            && trimmed.Split('/').All(segment => RouteSegmentRegex().IsMatch(segment));
    }

    /// <summary>Returns true for a CSS property or custom-property name.</summary>
    public static bool IsSafeCssPropertyName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 128
        && CssPropertyRegex().IsMatch(name);

    /// <summary>Rejects declaration breakout, legacy script CSS, and unsafe URL targets.</summary>
    public static bool IsSafeCssValue(string value)
    {
        if (value.Length > 8_192
            || value.IndexOfAny(['\0', '\r', '\n', '{', '}', ';']) >= 0
            || value.Contains("@import", StringComparison.OrdinalIgnoreCase)
            || value.Contains("expression(", StringComparison.OrdinalIgnoreCase)
            || value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("vbscript:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("-moz-binding", StringComparison.OrdinalIgnoreCase)
            || value.Contains("behavior:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (Match match in CssUrlRegex().Matches(value))
        {
            var target = match.Groups[1].Value.Trim().Trim('\'', '"');
            if (!IsSafeCssUrl(target))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns true when an attribute name cannot introduce script/style execution.</summary>
    public static bool IsSafeHtmlAttribute(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 128
            || !HtmlAttributeRegex().IsMatch(name)
            || name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
            || name.Equals("style", StringComparison.OrdinalIgnoreCase)
            || name.Equals("srcdoc", StringComparison.OrdinalIgnoreCase)
            || name.Equals("formaction", StringComparison.OrdinalIgnoreCase)
            || name.Equals("xlink:href", StringComparison.OrdinalIgnoreCase)
            || value.Length > 16_384)
        {
            return false;
        }

        return !UrlAttributes.Contains(name, StringComparer.OrdinalIgnoreCase) || IsSafeHtmlUrl(value, name);
    }

    private static bool IsSafeHtmlUrl(string value, string attribute)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.Contains('\\'))
        {
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return !trimmed.Split('/').Any(segment => segment == "..");
        }

        if (uri.Scheme is "https" or "http")
        {
            return true;
        }

        return attribute.Equals("href", StringComparison.OrdinalIgnoreCase)
            && uri.Scheme is "mailto" or "tel";
    }

    private static bool IsSafeCssUrl(string target)
    {
        if (target.StartsWith('#') || target.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return !target.Contains("..", StringComparison.Ordinal);
        }

        return target.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("data:image/avif;base64,", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex RouteSegmentRegex();

    [GeneratedRegex("^(?:--[A-Za-z_][A-Za-z0-9_-]*|-?[A-Za-z][A-Za-z0-9-]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex CssPropertyRegex();

    [GeneratedRegex("url\\(([^)]*)\\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssUrlRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9:_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlAttributeRegex();
}
