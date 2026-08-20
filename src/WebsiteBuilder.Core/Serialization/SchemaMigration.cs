using System.Text.Json.Nodes;

namespace WebsiteBuilder.Core.Serialization;

/// <summary>
/// A single forward schema-migration step that upgrades a Project JSON document
/// from <see cref="FromVersion"/> to <c>FromVersion + 1</c>, mutating it in place.
/// </summary>
public sealed record SchemaMigration(int FromVersion, string Description, Action<JsonObject> Apply);
