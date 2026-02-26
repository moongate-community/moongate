using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moongate.Server.Http.Data;
using Scalar.AspNetCore;

namespace Moongate.Server.Http.Extensions;

/// <summary>
/// Builder extensions for JWT and OpenAPI setup.
/// </summary>
internal static class MoongateHttpBuilderExtensions
{
    public static IServiceCollection ConfigureMoongateHttpJwt(
        this IServiceCollection services,
        MoongateHttpJwtOptions options
    )
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(
                jwtOptions =>
                {
                    jwtOptions.TokenValidationParameters = new()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = options.Issuer,
                        ValidAudience = options.Audience,
                        IssuerSigningKey = key,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                }
            );

        services.AddAuthorization();

        return services;
    }

    public static IApplicationBuilder MapMoongateOpenApiRoutes(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.Theme = ScalarTheme.BluePlanet);

        return app;
    }
}
