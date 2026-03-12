using Microsoft.AspNetCore.Builder;
using Moongate.Core.Data.Directories;
using Moongate.Server.Http.Data;
using Serilog.Events;

namespace Moongate.Server.Http;

/// <summary>
/// Configuration options for <see cref="MoongateHttpService" />.
/// </summary>
public sealed class MoongateHttpServiceOptions
{
    /// <summary>
    /// Shared directories configuration used by the server.
    /// </summary>
    public DirectoriesConfig? DirectoriesConfig { get; init; }

    /// <summary>
    /// HTTP listening port.
    /// </summary>
    public int Port { get; init; } = 8088;

    /// <summary>
    /// Enables OpenAPI endpoints.
    /// </summary>
    public bool IsOpenApiEnabled { get; init; } = true;

    /// <summary>
    /// Shard display name used by public branding endpoints.
    /// </summary>
    public string ShardName { get; init; } = "Moongate";

    /// <summary>
    /// Public admin login logo path relative to the mounted web root.
    /// </summary>
    public string? AdminLoginLogoPath { get; init; }

    /// <summary>
    /// Public player login logo path relative to the mounted web root.
    /// </summary>
    public string? PlayerLoginLogoPath { get; init; }

    /// <summary>
    /// Minimum log level for HTTP logs.
    /// </summary>
    public LogEventLevel MinimumLogLevel { get; init; } = LogEventLevel.Information;

    /// <summary>
    /// Optional callback for extra endpoint/middleware registrations.
    /// </summary>
    public Action<WebApplication>? ConfigureApp { get; init; }

    /// <summary>
    /// Optional JWT authentication options.
    /// </summary>
    public MoongateHttpJwtOptions? Jwt { get; init; }

    /// <summary>
    /// Enables serving the UI SPA from the HTTP service root path.
    /// </summary>
    public bool IsUiEnabled { get; init; } = true;

    /// <summary>
    /// Optional absolute or relative path to the UI dist directory.
    /// If null, the service tries common defaults like <c>./ui/dist</c>.
    /// </summary>
    public string? UiDistPath { get; init; }
}
