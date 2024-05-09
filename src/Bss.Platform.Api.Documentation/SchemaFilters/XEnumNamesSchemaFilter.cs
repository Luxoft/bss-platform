using System.Reflection;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bss.Platform.Api.Documentation.SchemaFilters;

/// <summary>
///     Adds names for enum types.
///     Needed for supporting correct front-end code generation by GenGen https://github.com/Luxoft/gengen
/// </summary>
public class XEnumNamesSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var typeInfo = context.Type.GetTypeInfo();
        if (!typeInfo.IsEnum)
        {
            return;
        }

        var openApiArray = new OpenApiArray();
        openApiArray.AddRange(Enum.GetNames(context.Type).Select(x => new OpenApiString(x)));

        schema.Extensions.Add("x-enumNames", openApiArray);
    }
}
