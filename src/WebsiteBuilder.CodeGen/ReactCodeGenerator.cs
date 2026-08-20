using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.CodeGen;

/// <summary>
/// Emits an npm-ready Next.js App Router project from a <see cref="Project"/>.
/// Pages become statically exportable Server Components, while the generated
/// project uses the same shared stylesheet as the static-HTML exporter.
/// </summary>
public sealed class ReactCodeGenerator : ICodeGenerator
{
    public const string NextVersion = "16.3.0";
    public const string ReactVersion = "19.2.8";
    public const string TypeScriptVersion = "7.0.2";

    private const string TransparentImage =
        "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=";

    private static readonly HashSet<string> BooleanAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "autoFocus", "autoPlay", "controls", "default", "defer", "disabled", "formNoValidate",
        "hidden", "loop", "multiple", "muted", "noValidate", "open", "playsInline", "readOnly",
        "required", "reversed", "scoped",
    };

    private static readonly HashSet<string> NumericAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cols", "colSpan", "high", "low", "maxLength", "minLength", "optimum", "rowSpan",
        "rows", "size", "span", "start",
    };

    private static readonly HashSet<string> SafeStringAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept", "accessKey", "alt", "autoComplete", "capture", "charSet", "cite", "content",
        "dateTime", "dir", "download", "encType", "form", "formEncType", "formMethod", "formTarget",
        "headers", "height", "hrefLang", "htmlFor", "id", "inputMode", "kind", "label", "lang",
        "list", "max", "media", "method", "min", "name", "nonce", "pattern", "placeholder", "preload",
        "rel", "role", "scope", "slot", "srcLang", "step", "target", "title", "translate", "value",
        "width", "wrap",
    };

    /// <inheritdoc />
    public CodeGenTarget Target => CodeGenTarget.React;

    /// <inheritdoc />
    public IReadOnlyList<GeneratedFile> Generate(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.Pages.Count == 0)
        {
            return Array.Empty<GeneratedFile>();
        }

        var sheet = WebStyleSheet.Build(project);
        var componentNames = BuildComponentNames(project.Pages);
        var routeDirectories = BuildRouteDirectories(project.Pages);
        var files = new List<GeneratedFile>
        {
            new("package.json", BuildPackageJson(project)),
            new("next.config.ts", BuildNextConfig()),
            new("tsconfig.json", BuildTsConfig()),
            new(".gitignore", BuildGitIgnore()),
            new("README.md", BuildReadme(project)),
            new("public/_headers", BuildSecurityHeaders()),
            new("app/globals.css", sheet.Css),
            new("app/layout.tsx", BuildRootLayout(project)),
            new("app/error.tsx", BuildErrorBoundary()),
            new("app/not-found.tsx", BuildNotFound()),
        };

        foreach (var page in project.Pages)
        {
            var directory = routeDirectories[page];
            var relativePath = directory.Length == 0
                ? "app/page.tsx"
                : $"app/{directory}/page.tsx";
            files.Add(new GeneratedFile(
                relativePath,
                BuildPageComponent(project, page, componentNames[page], sheet)));
        }

        return files;
    }

    private static string BuildPackageJson(Project project) =>
        "{\n" +
        $"  \"name\": {TsString(NpmName(project.Name))},\n" +
        "  \"private\": true,\n" +
        "  \"version\": \"0.0.0\",\n" +
        "  \"scripts\": {\n" +
        "    \"dev\": \"next dev\",\n" +
        "    \"build\": \"next build\",\n" +
        "    \"typecheck\": \"next typegen && tsc --noEmit\"\n" +
        "  },\n" +
        "  \"dependencies\": {\n" +
        $"    \"next\": \"{NextVersion}\",\n" +
        $"    \"react\": \"{ReactVersion}\",\n" +
        $"    \"react-dom\": \"{ReactVersion}\"\n" +
        "  },\n" +
        "  \"devDependencies\": {\n" +
        "    \"@types/node\": \"26.2.0\",\n" +
        "    \"@types/react\": \"19.2.18\",\n" +
        "    \"@types/react-dom\": \"19.2.4\",\n" +
        $"    \"typescript\": \"{TypeScriptVersion}\"\n" +
        "  },\n" +
        "  \"engines\": {\n" +
        "    \"node\": \">=20.9.0\"\n" +
        "  }\n" +
        "}\n";

    private static string BuildNextConfig() =>
        "import type { NextConfig } from \"next\";\n\n" +
        "const nextConfig = {\n" +
        "  output: \"export\",\n" +
        "  poweredByHeader: false,\n" +
        "  experimental: {\n" +
        "    // TypeScript 7 is native and has no legacy JavaScript compiler API.\n" +
        "    useTypeScriptCli: true,\n" +
        "  },\n" +
        "  images: {\n" +
        "    unoptimized: true,\n" +
        "  },\n" +
        "} satisfies NextConfig;\n\n" +
        "export default nextConfig;\n";

    private static string BuildTsConfig() =>
        "{\n" +
        "  \"compilerOptions\": {\n" +
        "    \"target\": \"ES2022\",\n" +
        "    \"lib\": [\"DOM\", \"DOM.Iterable\", \"ESNext\"],\n" +
        "    \"allowJs\": false,\n" +
        "    \"skipLibCheck\": true,\n" +
        "    \"strict\": true,\n" +
        "    \"noUncheckedIndexedAccess\": true,\n" +
        "    \"exactOptionalPropertyTypes\": true,\n" +
        "    \"noUnusedLocals\": true,\n" +
        "    \"noUnusedParameters\": true,\n" +
        "    \"noFallthroughCasesInSwitch\": true,\n" +
        "    \"noEmit\": true,\n" +
        "    \"esModuleInterop\": true,\n" +
        "    \"module\": \"ESNext\",\n" +
        "    \"moduleResolution\": \"Bundler\",\n" +
        "    \"resolveJsonModule\": true,\n" +
        "    \"isolatedModules\": true,\n" +
        "    \"jsx\": \"react-jsx\",\n" +
        "    \"incremental\": true,\n" +
        "    \"plugins\": [{ \"name\": \"next\" }]\n" +
        "  },\n" +
        "  \"include\": [\n" +
        "    \"next-env.d.ts\",\n" +
        "    \"app/**/*.ts\",\n" +
        "    \"app/**/*.tsx\",\n" +
        "    \".next/types/**/*.ts\",\n" +
        "    \".next/dev/types/**/*.ts\"\n" +
        "  ],\n" +
        "  \"exclude\": [\"node_modules\", \"out\"]\n" +
        "}\n";

    private static string BuildGitIgnore() =>
        "node_modules/\n" +
        ".next/\n" +
        "out/\n" +
        "*.tsbuildinfo\n" +
        ".env*\n" +
        "!.env.example\n";

    private static string BuildReadme(Project project) =>
        $"# {project.Name}\n\n" +
        "Generated by Likha - Website Builder.\n\n" +
        "```sh\n" +
        "npm install\n" +
        "npm run typecheck\n" +
        "npm run dev\n" +
        "npm run build\n" +
        "```\n\n" +
        "The production build is a static export in `out/`. Commit the generated " +
        "`package-lock.json` after the first install. `public/_headers` supplies secure " +
        "defaults on hosts that support the `_headers` convention; configure equivalent " +
        "headers on other platforms.\n";

    private static string BuildSecurityHeaders() =>
        "/*\n" +
        "  Content-Security-Policy: default-src 'self'; base-uri 'self'; connect-src 'self'; " +
        "font-src 'self' data:; form-action 'self'; frame-ancestors 'none'; img-src 'self' data: https:; " +
        "object-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'\n" +
        "  Permissions-Policy: camera=(), geolocation=(), microphone=()\n" +
        "  Referrer-Policy: strict-origin-when-cross-origin\n" +
        "  X-Content-Type-Options: nosniff\n" +
        "  X-Frame-Options: DENY\n";

    private static string BuildRootLayout(Project project) =>
        "import type { Metadata } from \"next\";\n" +
        "import type { ReactNode } from \"react\";\n" +
        "import \"./globals.css\";\n\n" +
        "export const metadata = {\n" +
        "  title: {\n" +
        $"    default: {TsString(project.Name)},\n" +
        $"    template: {TsString($"%s | {project.Name}")},\n" +
        "  },\n" +
        $"  description: {TsString($"{project.Name}, generated by Likha.")},\n" +
        "} satisfies Metadata;\n\n" +
        "export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {\n" +
        "  return (\n" +
        "    <html lang=\"en\">\n" +
        "      <body>{children}</body>\n" +
        "    </html>\n" +
        "  );\n" +
        "}\n";

    private static string BuildErrorBoundary() =>
        "\"use client\";\n\n" +
        "interface ErrorPageProps {\n" +
        "  error: Error & { digest?: string };\n" +
        "  reset: () => void;\n" +
        "}\n\n" +
        "export default function ErrorPage({ reset }: ErrorPageProps) {\n" +
        "  return (\n" +
        "    <main role=\"alert\">\n" +
        "      <h1>Something went wrong.</h1>\n" +
        "      <p>Please try loading this page again.</p>\n" +
        "      <button type=\"button\" onClick={reset}>Try again</button>\n" +
        "    </main>\n" +
        "  );\n" +
        "}\n";

    private static string BuildNotFound() =>
        "export default function NotFound() {\n" +
        "  return (\n" +
        "    <main>\n" +
        "      <h1>Page not found</h1>\n" +
        "      <p>The requested page does not exist.</p>\n" +
        "    </main>\n" +
        "  );\n" +
        "}\n";

    private static string BuildPageComponent(
        Project project,
        Page page,
        string componentName,
        WebStyleSheet sheet)
    {
        var sb = new StringBuilder();
        sb.Append("import type { Metadata } from \"next\";\n");
        if (ContainsType(page.Root, ElementTypes.Image))
        {
            sb.Append("import Image from \"next/image\";\n");
        }

        sb.Append("\nexport const metadata = {\n");
        sb.Append("  title: ").Append(TsString(page.Name)).Append(",\n");
        sb.Append("  description: ")
          .Append(TsString($"{page.Name} page for {project.Name}."))
          .Append(",\n");
        sb.Append("} satisfies Metadata;\n\n");
        sb.Append("export default function ").Append(componentName).Append("() {\n");
        sb.Append("  return (\n");
        AppendJsx(sb, page.Root, depth: 2, rootClass: WebStyleSheet.RootClass(page), sheet);
        sb.Append("  );\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static void AppendJsx(
        StringBuilder sb,
        ElementNode node,
        int depth,
        string? rootClass,
        WebStyleSheet sheet)
    {
        var isRoot = rootClass is not null;
        var indent = new string(' ', depth * 2);
        var className = isRoot ? rootClass! : sheet.ElementClass(node);

        if (node.Type == ElementTypes.Image)
        {
            AppendImage(sb, node, indent, className);
            return;
        }

        var tag = HtmlTags.TagFor(node.Type);
        var attributes = BuildJsxAttributes(node, className);
        if (HtmlTags.IsVoid(tag))
        {
            sb.Append(indent).Append('<').Append(tag).Append(attributes).Append(" />\n");
            return;
        }

        var hasText = !string.IsNullOrEmpty(node.Text);
        var hasChildren = node.Children.Count > 0;
        if (!hasText && !hasChildren)
        {
            sb.Append(indent).Append('<').Append(tag).Append(attributes)
              .Append("></").Append(tag).Append(">\n");
            return;
        }

        if (hasText && !hasChildren)
        {
            sb.Append(indent).Append('<').Append(tag).Append(attributes).Append('>')
              .Append('{').Append(TsString(node.Text!)).Append('}')
              .Append("</").Append(tag).Append(">\n");
            return;
        }

        sb.Append(indent).Append('<').Append(tag).Append(attributes).Append(">\n");
        if (hasText)
        {
            sb.Append(new string(' ', (depth + 1) * 2))
              .Append('{').Append(TsString(node.Text!)).Append("}\n");
        }

        foreach (var child in node.Children)
        {
            AppendJsx(sb, child, depth + 1, rootClass: null, sheet);
        }

        sb.Append(indent).Append("</").Append(tag).Append(">\n");
    }

    private static void AppendImage(StringBuilder sb, ElementNode node, string indent, string className)
    {
        var src = node.Attributes.TryGetValue("src", out var candidate)
            && IsSafeUrl("src", candidate)
                ? candidate
                : TransparentImage;
        var alt = node.Attributes.TryGetValue("alt", out var altText)
            ? altText
            : node.Name ?? string.Empty;
        var width = Math.Max(1, (int)Math.Round(node.Width, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(node.Height, MidpointRounding.AwayFromZero));
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "src", "alt", "width", "height",
        };

        sb.Append(indent).Append("<Image")
          .Append(BuildJsxAttributes(node, className, excluded))
          .Append(" src=").Append(TsString(src))
          .Append(" alt=").Append(TsString(alt))
          .Append(" width={").Append(width.ToString(CultureInfo.InvariantCulture)).Append('}')
          .Append(" height={").Append(height.ToString(CultureInfo.InvariantCulture)).Append('}')
          .Append(" unoptimized />\n");
    }

    private static string BuildJsxAttributes(
        ElementNode node,
        string className,
        ISet<string>? excluded = null)
    {
        var sb = new StringBuilder();
        sb.Append(" className=").Append(TsString(className));

        foreach (var (rawName, value) in node.Attributes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (excluded?.Contains(rawName) == true
                || !TryNormalizeAttribute(rawName, out var name)
                || !TryFormatAttribute(name, value, out var formatted))
            {
                continue;
            }

            sb.Append(' ').Append(name).Append('=').Append(formatted);
        }

        return sb.ToString();
    }

    private static bool TryNormalizeAttribute(string rawName, out string name)
    {
        name = rawName switch
        {
            "for" => "htmlFor",
            "readonly" => "readOnly",
            "tabindex" => "tabIndex",
            "maxlength" => "maxLength",
            "minlength" => "minLength",
            "colspan" => "colSpan",
            "rowspan" => "rowSpan",
            _ => rawName,
        };

        if (!IsSafeAttributeName(name)
            || name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
            || name.Equals("class", StringComparison.OrdinalIgnoreCase)
            || name.Equals("className", StringComparison.OrdinalIgnoreCase)
            || name.Equals("style", StringComparison.OrdinalIgnoreCase)
            || name.Equals("children", StringComparison.OrdinalIgnoreCase)
            || name.Equals("dangerouslySetInnerHTML", StringComparison.OrdinalIgnoreCase)
            || name.Equals("key", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ref", StringComparison.OrdinalIgnoreCase))
        {
            name = string.Empty;
            return false;
        }

        return true;
    }

    private static bool TryFormatAttribute(string name, string value, out string formatted)
    {
        if (name.StartsWith("data-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase)
            || SafeStringAttributes.Contains(name))
        {
            formatted = TsString(value);
            return true;
        }

        if (name is "href" or "src" or "action" or "formAction" or "poster")
        {
            if (IsSafeUrl(name, value))
            {
                formatted = TsString(value);
                return true;
            }

            formatted = string.Empty;
            return false;
        }

        if (BooleanAttributes.Contains(name))
        {
            var truthy = value.Length == 0
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals(name, StringComparison.OrdinalIgnoreCase);
            formatted = truthy ? "{true}" : "{false}";
            return true;
        }

        if (NumericAttributes.Contains(name)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number))
        {
            formatted = "{" + number.ToString("0.####", CultureInfo.InvariantCulture) + "}";
            return true;
        }

        if (name.Equals("type", StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9-]*$", RegexOptions.CultureInvariant))
        {
            formatted = TsString(value.ToLowerInvariant());
            return true;
        }

        formatted = string.Empty;
        return false;
    }

    private static bool IsSafeUrl(string name, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsControl))
        {
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            return !trimmed.StartsWith("//", StringComparison.Ordinal);
        }

        if (name is "action" or "formAction")
        {
            return false;
        }

        if (name is "src" or "poster")
        {
            return absolute.Scheme == Uri.UriSchemeHttps
                || (absolute.Scheme == "data"
                    && trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase));
        }

        return absolute.Scheme == Uri.UriSchemeHttp
            || absolute.Scheme == Uri.UriSchemeHttps
            || absolute.Scheme is "mailto" or "tel";
    }

    private static bool IsSafeAttributeName(string name) =>
        name.Length > 0 && name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    private static bool ContainsType(ElementNode node, string type) =>
        node.Type == type || node.Children.Any(child => ContainsType(child, type));

    private static IReadOnlyDictionary<Page, string> BuildComponentNames(IReadOnlyList<Page> pages)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var map = new Dictionary<Page, string>();
        foreach (var page in pages)
        {
            var baseName = PascalName(page) + "Page";
            var name = baseName;
            var suffix = 2;
            while (!used.Add(name))
            {
                name = baseName + suffix++.ToString(CultureInfo.InvariantCulture);
            }

            map[page] = name;
        }

        return map;
    }

    private static IReadOnlyDictionary<Page, string> BuildRouteDirectories(IReadOnlyList<Page> pages)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<Page, string>();
        foreach (var page in pages)
        {
            var desired = NormalizeRoute(page.Route);
            var candidate = desired;
            var suffix = 2;
            while (!used.Add(candidate))
            {
                candidate = desired.Length == 0
                    ? "page-" + suffix.ToString(CultureInfo.InvariantCulture)
                    : desired + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            map[page] = candidate;
        }

        return map;
    }

    private static string NormalizeRoute(string route)
    {
        var raw = string.IsNullOrWhiteSpace(route) ? "index" : route.Trim();
        if (raw.Equals("index", StringComparison.OrdinalIgnoreCase) || raw == "/")
        {
            return string.Empty;
        }

        var segments = raw.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safe = segments.Select(segment =>
        {
            var slug = Regex.Replace(segment, "[^A-Za-z0-9_-]+", "-", RegexOptions.CultureInvariant)
                .Trim('-', '_');
            return slug.Length == 0 ? "page" : slug;
        });
        return string.Join('/', safe);
    }

    private static string PascalName(Page page)
    {
        var source = !string.IsNullOrWhiteSpace(page.Name) ? page.Name : WebStyleSheet.FileStem(page);
        var words = Regex.Split(source, "[^A-Za-z0-9]+", RegexOptions.CultureInvariant)
            .Where(word => word.Length > 0);
        var pascal = string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
        if (pascal.Length == 0 || !char.IsLetter(pascal[0]))
        {
            pascal = "Page" + pascal;
        }

        return pascal;
    }

    private static string TsString(string value) => JsonSerializer.Serialize(value);

    private static string NpmName(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')
            .ToArray();
        var result = Regex.Replace(new string(chars), "-+", "-", RegexOptions.CultureInvariant)
            .Trim('-');
        if (result.Length == 0)
        {
            return "likha-site";
        }

        return result.Length <= 214 ? result : result[..214].TrimEnd('-');
    }
}
