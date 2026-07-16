using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces;
using Scalar.AspNetCore;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;

namespace Moongate.Http.Plugin.Services;

/// <summary>
/// Runs the REST API. SquidStd hosts the process, so this owns the <see cref="WebApplication" />'s
/// lifetime rather than the other way round — which is what keeps the whole plugin optional: drop its
/// registration and the game server boots with no web stack at all.
/// </summary>
public sealed class HttpServerService : ISquidStdService
{
    /// <summary>Administrator or GrandMaster: staff, not players.</summary>
    public const string AdminPolicy = "admin";

    /// <summary>Any authenticated account.</summary>
    public const string PlayerPolicy = "player";

    /// <summary>Where Swashbuckle publishes the document, and so where Scalar is pointed to read it.</summary>
    public const string SwaggerDocumentRoute = "/swagger/v1/swagger.json";

    private readonly ILogger _logger = Log.ForContext<HttpServerService>();
    private readonly IContainer _container;
    private readonly MoongateHttpConfig _config;

    private WebApplication? _app;

    public HttpServerService(IContainer container, MoongateHttpConfig config)
    {
        _container = container;
        _config = config;
    }

    /// <summary>The port actually listened on — the real one when the configured port is 0.</summary>
    public int BoundPort { get; private set; }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        // Validated before anything binds, so a missing key is a startup error rather than a 500 on the
        // first login attempt.
        var signingKey = JwtTokenService.SigningKey(_config.Jwt.SigningKey);

        var builder = WebApplication.CreateBuilder();

        // The game's own container, so endpoints resolve the very singletons the game loop holds.
        builder.Host.UseServiceProviderFactory(new DryIocServiceProviderFactory(_container));
        builder.WebHost.UseUrls($"http://{_config.Address}:{_config.Port}");

        builder.Services.AddProblemDetails();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new()
                    {
                        ValidateIssuer = true,
                        ValidIssuer = _config.Jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = _config.Jwt.Issuer,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = signingKey,
                        ValidateLifetime = true
                    };
                }
            );

        builder.Services
            .AddAuthorizationBuilder()
            .AddPolicy(
                AdminPolicy,
                policy => policy.RequireRole(
                    nameof(AccountLevelType.Administrator),
                    nameof(AccountLevelType.GrandMaster)
                )
            )
            .AddPolicy(PlayerPolicy, policy => policy.RequireAuthenticatedUser());

        _app = builder.Build();

        _app.UseExceptionHandler();
        _app.UseStatusCodePages();
        _app.UseAuthentication();
        _app.UseAuthorization();

        _app.UseSwagger();
        _app.MapScalarApiReference(options => options.AddDocument("v1", routePattern: SwaggerDocumentRoute));
        _app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

        foreach (var endpoints in _container.Resolve<IEnumerable<IApiEndpointRegistration>>())
        {
            endpoints.Register(_app);
        }

        await _app.StartAsync(cancellationToken);

        BoundPort = ResolveBoundPort(_app);

        _logger.Information("REST API listening on http://{Address}:{Port}", _config.Address, BoundPort);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync(cancellationToken);
        await _app.DisposeAsync();
        _app = null;
    }

    /// <summary>
    /// Asks Kestrel what it actually bound. With a configured port of 0 the OS picks one, and this is the
    /// only way to learn it.
    /// </summary>
    private static int ResolveBoundPort(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var address = addresses?.Addresses.FirstOrDefault();

        return address is null ? 0 : new Uri(address).Port;
    }
}
