using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class SecurityDocumentTransformer : IOpenApiDocumentTransformer
    {
        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
            {
                {
                    "Bearer", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "BearerToken"
                    }
                }
            };
            document.SecurityRequirements = [
                new()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                }
            ];
            await Task.CompletedTask;
        }
    }
}
