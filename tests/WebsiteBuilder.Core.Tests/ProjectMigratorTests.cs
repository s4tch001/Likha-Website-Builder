using System.Text.Json.Nodes;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Serialization;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

/// <summary>
/// Covers the schema-migration pipeline: legacy documents are upgraded to the
/// current version, current documents pass through untouched, and newer-than-
/// supported documents are rejected.
/// </summary>
public class ProjectMigratorTests
{
    [Fact]
    public void ReadVersion_TreatsMissingVersionAsZero()
    {
        var doc = new JsonObject { ["name"] = "Legacy" };
        Assert.Equal(0, ProjectMigrator.ReadVersion(doc));
    }

    [Fact]
    public void Migrate_LegacyDocument_StampsVersionAndBackfillsBreakpoints()
    {
        // No schemaVersion and an empty breakpoint list (a hand-authored / v0 file).
        var doc = new JsonObject
        {
            ["name"] = "Legacy",
            ["breakpoints"] = new JsonArray(),
        };

        ProjectMigrator.Migrate(doc);

        Assert.Equal(Project.CurrentSchemaVersion, ProjectMigrator.ReadVersion(doc));
        var breakpoints = Assert.IsType<JsonArray>(doc["breakpoints"]);
        Assert.Equal(Breakpoint.Defaults.Count, breakpoints.Count);
        Assert.NotNull(doc["variables"]);
        Assert.NotNull(doc["assets"]);
    }

    [Fact]
    public void Migrate_CurrentDocument_LeavesBreakpointsUntouched()
    {
        var doc = new JsonObject
        {
            ["schemaVersion"] = Project.CurrentSchemaVersion,
            ["breakpoints"] = new JsonArray(
                new JsonObject { ["id"] = "only", ["label"] = "Only", ["maxWidth"] = 0, ["isBase"] = true }),
        };

        ProjectMigrator.Migrate(doc);

        var breakpoints = Assert.IsType<JsonArray>(doc["breakpoints"]);
        Assert.Single(breakpoints);
    }

    [Fact]
    public void Migrate_VersionOneDocument_AddsEmptyAssetCollection()
    {
        var doc = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["assets"] = null,
        };

        ProjectMigrator.Migrate(doc);

        Assert.Equal(Project.CurrentSchemaVersion, ProjectMigrator.ReadVersion(doc));
        Assert.Empty(Assert.IsType<JsonArray>(doc["assets"]));
    }

    [Fact]
    public void Migrate_NewerThanSupported_Throws()
    {
        var doc = new JsonObject { ["schemaVersion"] = Project.CurrentSchemaVersion + 1 };
        Assert.Throws<InvalidDataException>(() => ProjectMigrator.Migrate(doc));
    }

    [Fact]
    public void Deserialize_LegacyJson_UpgradesAndBinds()
    {
        // A minimal legacy document: no schemaVersion, no breakpoints.
        const string legacy =
            """
            { "name": "Old Site", "pages": [ { "id": "p1", "name": "Home", "route": "index",
              "root": { "id": "root", "type": "Section" } } ] }
            """;

        var project = ProjectSerializer.Deserialize(legacy);

        Assert.Equal("Old Site", project.Name);
        Assert.Equal(Project.CurrentSchemaVersion, project.SchemaVersion);
        Assert.Equal(Breakpoint.Defaults.Count, project.Breakpoints.Count);
        Assert.Contains(project.Breakpoints, b => b.IsBase);
        Assert.Empty(project.Assets);
    }
}
