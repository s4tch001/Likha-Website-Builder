using WebsiteBuilder.Core.Components;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Validation;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

public sealed class ComponentDefinitionTests
{
    [Fact]
    public void BuiltInLibrary_HasUniqueValidatedDefinitions()
    {
        var definitions = BuiltInComponentLibrary.All;
        Assert.NotEmpty(definitions);
        Assert.Equal(definitions.Count, definitions.Select(item => item.Id).Distinct().Count());
        foreach (var definition in definitions)
        {
            ComponentDefinitionValidator.ValidateAndThrow(definition);
        }
    }

    [Fact]
    public void Validator_RejectsUnsafeTemplateContent()
    {
        var definition = new ComponentDefinition(
            "unsafe-block",
            "Unsafe",
            "Test",
            "Invalid event attribute.",
            "!",
            ["test"],
            new ElementNode
            {
                Id = "bad",
                Type = ElementTypes.Image,
                Attributes = { ["onerror"] = "alert(1)" },
            });

        Assert.Throws<ProjectValidationException>(() =>
            ComponentDefinitionValidator.ValidateAndThrow(definition));
    }
}
