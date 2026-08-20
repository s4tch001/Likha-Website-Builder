using System.IO;
using System.Text.Json.Nodes;
using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Serialization;

/// <summary>
/// Brings a Project JSON document up to <see cref="Project.CurrentSchemaVersion"/> by
/// applying the registered <see cref="SchemaMigration"/> steps in order. Older files
/// are upgraded in memory before deserialization so the rest of the app only ever
/// sees the current shape; files authored by a newer schema are rejected with a
/// clear error rather than being silently misread.
/// </summary>
public static class ProjectMigrator
{
    /// <summary>Forward migrations, each keyed by the version it upgrades <em>from</em>.</summary>
    public static IReadOnlyList<SchemaMigration> Migrations { get; } = new[]
    {
        new SchemaMigration(0, "Stamp schema version and backfill default breakpoints.", MigrateV0ToV1),
        new SchemaMigration(1, "Add the managed asset metadata collection.", MigrateV1ToV2),
    };

    /// <summary>Reads a document's schema version; a missing/blank value is treated as 0 (legacy).</summary>
    public static int ReadVersion(JsonObject doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        if (doc.TryGetPropertyValue("schemaVersion", out var node)
            && node is not null
            && int.TryParse(node.ToString(), out var version))
        {
            return version;
        }

        return 0;
    }

    /// <summary>
    /// Upgrades <paramref name="doc"/> in place to the current schema version and
    /// returns it. Throws <see cref="InvalidDataException"/> if the document is newer
    /// than supported, or if no migration is registered for an intermediate version.
    /// </summary>
    public static JsonObject Migrate(JsonObject doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var version = ReadVersion(doc);
        if (version > Project.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"This project was created with a newer version of the app " +
                $"(schema v{version}; this app supports up to v{Project.CurrentSchemaVersion}). " +
                "Please update the application to open it.");
        }

        while (version < Project.CurrentSchemaVersion)
        {
            var migration = Migrations.FirstOrDefault(m => m.FromVersion == version)
                ?? throw new InvalidDataException(
                    $"No migration is registered from schema version {version}.");

            migration.Apply(doc);
            version++;
            doc["schemaVersion"] = version;
        }

        return doc;
    }

    /// <summary>
    /// v0 → v1: legacy / hand-authored documents may omit the breakpoint set or
    /// leave it empty; backfill the standard breakpoints so the responsive engine
    /// has a base to cascade from, and ensure the variables map exists.
    /// </summary>
    private static void MigrateV0ToV1(JsonObject doc)
    {
        if (doc["breakpoints"] is not JsonArray breakpoints || breakpoints.Count == 0)
        {
            var array = new JsonArray();
            foreach (var bp in Breakpoint.Defaults)
            {
                array.Add(new JsonObject
                {
                    ["id"] = bp.Id,
                    ["label"] = bp.Label,
                    ["maxWidth"] = bp.MaxWidth,
                    ["isBase"] = bp.IsBase,
                });
            }

            doc["breakpoints"] = array;
        }

        doc["variables"] ??= new JsonObject();
    }

    /// <summary>v1 → v2: assets were previously discovered directly from disk.</summary>
    private static void MigrateV1ToV2(JsonObject doc)
    {
        doc["assets"] ??= new JsonArray();
    }
}
