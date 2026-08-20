using System.Text.Json;
using System.Text.Json.Nodes;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Validation;

namespace WebsiteBuilder.Core.Serialization;

/// <summary>
/// Central (de)serialization for the Project JSON document. Exposes a single,
/// shared <see cref="JsonSerializerOptions"/> instance so the App, the Bridge
/// and the code generators all read and write byte-identical JSON.
/// </summary>
public static class ProjectSerializer
{
    /// <summary>Shared options: camelCase-friendly, indented, tolerant of trailing commas/comments on read.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Serialize(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ProjectValidator.ValidateAndThrow(project);
        return JsonSerializer.Serialize(project, Options);
    }

    public static Project Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Project JSON is empty.", nameof(json));
        }

        if (json.Length > ProjectValidator.MaxJsonCharacters)
        {
            throw new ProjectValidationException("Project JSON exceeds the maximum supported size.");
        }

        // Parse to a mutable node first so older documents can be migrated forward
        // (and newer-than-supported files rejected) before binding to the model.
        var node = JsonNode.Parse(json, nodeOptions: null, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (node is not JsonObject doc)
        {
            throw new InvalidDataException("Project JSON must be an object.");
        }

        ProjectMigrator.Migrate(doc);

        var project = doc.Deserialize<Project>(Options)
            ?? throw new InvalidDataException("Project JSON deserialized to null.");
        ProjectValidator.ValidateAndThrow(project);
        return project;
    }
}
