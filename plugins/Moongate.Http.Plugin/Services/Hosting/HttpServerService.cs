using System.Text.Json;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Auth;
using Scalar.AspNetCore;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Interfaces.Config;
using ILogger = Serilog.ILogger;

namespace Moongate.Http.Plugin.Services.Hosting;

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

    /// <summary>The v1 document's route, as Swashbuckle publishes it.</summary>
    public const string SwaggerDocumentRoute = "/swagger/v1/swagger.json";

    /// <summary>
    /// The same route as a pattern, which is what Scalar needs: it substitutes the document name per
    /// document. Without it Scalar looks for its own default, <c>openapi/{documentName}.json</c>, which
    /// nothing serves here — the page still renders, and shows an empty reference.
    /// </summary>
    private const string SwaggerRoutePattern = "/swagger/{documentName}/swagger.json";

    /// <summary>The only document served. Scalar's reference for it lives at <c>/scalar/v1</c>.</summary>
    private const string DocumentName = "v1";

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
        EnsureSigningKey();

        // Validated before anything binds, so a bad key is a startup error rather than a 500 on the first
        // login attempt.
        var signingKey = JwtTokenService.SigningKey(_config.Jwt.SigningKey);

        var builder = WebApplication.CreateBuilder();

        // The game's own container, so endpoints resolve the very singletons the game loop holds.
        builder.Host.UseServiceProviderFactory(new DryIocServiceProviderFactory(_container));
        builder.Logging.ClearProviders().AddSerilog(_logger);
        builder.WebHost.UseUrls($"http://{_config.Address}:{_config.Port}");

        builder.Services.AddProblemDetails();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(
            options =>
            {
                foreach (var path in EndpointXmlDocumentationPaths())
                {
                    options.IncludeXmlComments(path);
                }
            }
        );

        // camelCase over the wire while the DTOs stay PascalCase in C#. ASP.NET already defaults to this,
        // but the wire format is a contract clients are written against: stated here it cannot be changed
        // by a default shifting under us.
        builder.Services.ConfigureHttpJsonOptions(
            options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        );

        builder.Services
               .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
               .AddJwtBearer(
                   options =>
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
        _app.MapScalarApiReference(
            options =>
            {
                options.WithOpenApiRoutePattern(SwaggerRoutePattern);
                options.AddDocument(DocumentName);
            }
        );
        _app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

        _app.MapGet("/", () => Results.Ok(new { message = "Welcome to Moongate API" })).ExcludeFromDescription();

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

        // StopAsync releases the listener and the port; DisposeAsync would go further and dispose the
        // WebApplication's service provider — which is the game's container, since that is what the app
        // was built on. Its singletons are the game's, not the API's, and disposing them here takes them
        // down while the game is still running: the services that stop after this one then fail on
        // objects already disposed. The container's lifetime belongs to the bootstrap that owns it.
        await _app.StopAsync(cancellationToken);
        _app = null;
    }

    /// <summary>
    /// Finds the XML documentation of every assembly that contributes endpoints, which is what turns their
    /// <c>///</c> summaries into the descriptions Scalar renders.
    /// <para>
    /// Read from the DI registrations rather than a hardcoded list because the endpoints live in
    /// Moongate.Server while this runs in the plugin, and the dependency points the other way — the plugin
    /// cannot name the assembly it must document. Registration metadata gives the implementation types
    /// without resolving them, so nothing is constructed here just to be asked what assembly it came from.
    /// </para>
    /// <para>
    /// A missing file is skipped rather than passed on: Swashbuckle throws on one, and it throws while
    /// serving the document, so a single absent XML would turn the whole reference into a 500 instead of a
    /// page with some prose missing.
    /// </para>
    /// </summary>
    private IEnumerable<string> EndpointXmlDocumentationPaths()
        => _container.GetServiceRegistrations()
                     .Where(registration => registration.ServiceType == typeof(IApiEndpointRegistration))
                     .Select(registration => registration.ImplementationType?.Assembly)
                     .Where(assembly => assembly is not null)
                     .Select(assembly => Path.Combine(AppContext.BaseDirectory, $"{assembly!.GetName().Name}.xml"))
                     .Distinct()
                     .Where(File.Exists);

    /// <summary>
    /// Mints a signing key for a server that has not configured one, and writes it to moongate.yaml.
    /// <para>
    /// Persisting matters as much as generating: a key regenerated on every boot would invalidate every
    /// token issued before the restart, silently. Saving here rather than in the plugin's Configure is
    /// deliberate — Configure runs before the other config sections are registered, and the file is composed
    /// from the registered ones, so an early save would drop them.
    /// </para>
    /// <para>
    /// Only an absent key is filled in. A key that is set but unusable is left to fail loudly below: not
    /// configuring one is an omission worth covering, whereas configuring a bad one is a mistake worth
    /// reporting.
    /// </para>
    /// </summary>
    private void EnsureSigningKey()
    {
        if (!string.IsNullOrWhiteSpace(_config.Jwt.SigningKey))
        {
            return;
        }

        _config.Jwt.SigningKey = JwtTokenService.GenerateSigningKey();
        _container.Resolve<IConfigManagerService>().Save();

        _logger.Information("No http.Jwt.SigningKey was configured; minted one and saved it to moongate.yaml");
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
