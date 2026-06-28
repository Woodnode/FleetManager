using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FleetManager.Api.Infrastructure;

/// <summary>
/// Generates one Swagger document per API version discovered by Asp.Versioning.
/// </summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        => _provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title       = "FleetManager API",
                Version     = description.GroupName,
                Description = description.IsDeprecated
                    ? "API de gestion de flotte automobile multi-enseignes [DEPRECATED]"
                    : "API de gestion de flotte automobile multi-enseignes"
            });
        }
    }
}
