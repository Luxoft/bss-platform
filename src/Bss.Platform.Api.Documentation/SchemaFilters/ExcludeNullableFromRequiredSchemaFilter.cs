using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bss.Platform.Api.Documentation.SchemaFilters;

/// <summary>
///     Exclude nullable property from required.
///     Needed for supporting correct front-end code generation by GenGen https://github.com/Luxoft/gengen
///     Additional to https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2036
/// </summary>
public class ExcludeNullableFromRequiredSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Required.Count == 0)
        {
            return;
        }

        var nullableProperties = context.Type.GetProperties()
            .Where(x => !x.IsNonNullableReferenceType())
            .Select(x => x.Name);

        var requiredNullableProperty = schema.Required
            .Intersect(nullableProperties, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var property in requiredNullableProperty)
        {
            schema.Required.Remove(property);
        }
    }
}
