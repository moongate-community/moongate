using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Extensions;
using Moongate.Server.Http.Interfaces;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Metrics.Data;
using Moongate.UO.Data.Interfaces.Art;
using Moongate.UO.Data.Interfaces.Maps;
using Moongate.UO.Data.Interfaces.Templates;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Server.Http;

/// <summary>
/// Hosts a lightweight HTTP endpoint surface for diagnostics and admin APIs.
/// </summary>
public sealed class MoongateHttpService : IMoongateHttpService
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly int _port;
    private readonly bool _isOpenApiEnabled;
    private readonly LogEventLevel _minimumLogLevel;
    private readonly Action<WebApplication> _configureApp;
    private readonly MoongateHttpJwtOptions _jwtOptions;
    private readonly IAccountService? _accountService;
    private readonly ICharacterService? _characterService;
    private readonly IMetricsHttpSnapshotFactory? _metricsHttpSnapshotFactory;
    private readonly IItemTemplateService? _itemTemplateService;
    private readonly IArtService? _artService;
    private readonly IGameNetworkSessionService? _gameNetworkSessionService;
    private readonly ICommandSystemService? _commandSystemService;
    private readonly IMapImageService? _mapImageService;
    private readonly IHelpTicketService? _helpTicketService;
    private readonly bool _isUiEnabled;
    private readonly string? _uiDistPath;
    private readonly MoongateHttpBranding _branding;

    private WebApplication? _app;

    public MoongateHttpService(
        MoongateHttpServiceOptions options,
        IAccountService? accountService = null,
        ICharacterService? characterService = null,
        IMetricsHttpSnapshotFactory? metricsHttpSnapshotFactory = null,
        IItemTemplateService? itemTemplateService = null,
        IArtService? artService = null,
        IGameNetworkSessionService? gameNetworkSessionService = null,
        ICommandSystemService? commandSystemService = null,
        IMapImageService? mapImageService = null,
        IHelpTicketService? helpTicketService = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        _directoriesConfig = options.DirectoriesConfig ??
                             throw new ArgumentException("DirectoriesConfig must be provided.", nameof(options));

        if (options.Port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in range 1-65535.");
        }

        _port = options.Port;
        _isOpenApiEnabled = options.IsOpenApiEnabled;
        _minimumLogLevel = options.MinimumLogLevel;
        _configureApp = options.ConfigureApp ?? (_ => { });
        _jwtOptions = options.Jwt ?? new MoongateHttpJwtOptions();
        _accountService = accountService;
        _characterService = characterService;
        _metricsHttpSnapshotFactory = metricsHttpSnapshotFactory;
        _itemTemplateService = itemTemplateService;
        _artService = artService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _commandSystemService = commandSystemService;
        _mapImageService = mapImageService;
        _helpTicketService = helpTicketService;
        _isUiEnabled = options.IsUiEnabled;
        _uiDistPath = options.UiDistPath;
        _branding = new()
        {
            ShardName = string.IsNullOrWhiteSpace(options.ShardName) ? "Moongate" : options.ShardName,
            AdminLoginLogoUrl = NormalizePublicAssetPath(options.AdminLoginLogoPath),
            PlayerLoginLogoUrl = NormalizePublicAssetPath(options.PlayerLoginLogoPath)
        };

        if (_jwtOptions.IsEnabled && string.IsNullOrWhiteSpace(_jwtOptions.SigningKey))
        {
            throw new ArgumentException("JWT signing key must be configured when JWT is enabled.", nameof(options));
        }

        if (_jwtOptions.IsEnabled && _accountService is null)
        {
            throw new ArgumentException("IAccountService must be configured when JWT is enabled.", nameof(options));
        }
    }

    public async Task StartAsync()
    {
        if (_app is not null)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder([]);
        var logPath = CreateLogPath(_directoriesConfig[DirectoryType.Logs]);
        var httpLogger = CreateHttpLogger(logPath, _minimumLogLevel);

        builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");
        builder.Host.UseSerilog(httpLogger, true);
        builder.Services.AddResponseCompression(
            options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml"]);
            }
        );
        builder.Services.ConfigureHttpJsonOptions(
            options => { options.SerializerOptions.TypeInfoResolverChain.Insert(0, MoongateHttpJsonContext.Default); }
        );

        if (_jwtOptions.IsEnabled)
        {
            builder.Services.ConfigureMoongateHttpJwt(_jwtOptions);
        }

        if (_isOpenApiEnabled)
        {
            builder.Services.AddOpenApi();
        }

        var app = builder.Build();
        app.UseSerilogRequestLogging();
        app.UseResponseCompression();
        var isUiServing = ConfigureUiHosting(app);

        if (_jwtOptions.IsEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        var routeContext = new MoongateHttpRouteContext(
            _jwtOptions,
            _branding,
            _accountService,
            _characterService,
            _metricsHttpSnapshotFactory,
            isUiServing,
            _directoriesConfig,
            _itemTemplateService,
            _artService,
            _gameNetworkSessionService,
            _commandSystemService,
            _mapImageService,
            _helpTicketService
        );

        app.MapMoongateHttpRoutes(routeContext);

        if (_isOpenApiEnabled)
        {
            app.MapMoongateOpenApiRoutes();
        }

        _configureApp(app);

        await app.StartAsync();
        Log.Information("Moongate HTTP service started on port {Port}", _port);

        if (_isOpenApiEnabled)
        {
            Log.Information("OpenAPI documentation available at /scalar");
        }

        _app = app;
    }

    public async Task StopAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    internal static string BuildPrometheusPayload(MoongateHttpMetricsSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# generated by moongate");
        sb.Append("# collected_at_unix_ms ")
          .AppendLine(snapshot.CollectedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        var groupedMetrics = snapshot.Metrics
                                     .GroupBy(static pair => NormalizeMetricName(pair.Key))
                                     .OrderBy(static g => g.Key, StringComparer.Ordinal);

        foreach (var metricGroup in groupedMetrics)
        {
            var firstMetric = metricGroup.First();
            var metricType = firstMetric.Value.Type;
            var helpText = firstMetric.Value.Help ?? $"Moongate {metricGroup.Key} metric";

            sb.Append("# HELP moongate_")
              .Append(metricGroup.Key)
              .Append(' ')
              .AppendLine(helpText);

            sb.Append("# TYPE moongate_")
              .Append(metricGroup.Key)
              .Append(' ')
              .AppendLine(GetPrometheusTypeName(metricType));

            foreach (var (_, metric) in metricGroup.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                sb.Append("moongate_")
                  .Append(metricGroup.Key);

                if (metric.Tags is not null && metric.Tags.Count > 0)
                {
                    sb.Append('{');
                    var firstLabel = true;

                    foreach (var (labelKey, labelValue) in metric.Tags.OrderBy(
                                 static pair => pair.Key,
                                 StringComparer.Ordinal
                             ))
                    {
                        if (!firstLabel)
                        {
                            sb.Append(',');
                        }

                        sb.Append(NormalizeLabelName(labelKey))
                          .Append("=\"")
                          .Append(EscapeLabelValue(labelValue))
                          .Append('"');
                        firstLabel = false;
                    }

                    sb.Append('}');
                }

                sb.Append(' ')
                  .Append(metric.Value.ToString(CultureInfo.InvariantCulture));

                if (metric.Timestamp.HasValue)
                {
                    sb.Append(' ')
                      .Append(metric.Timestamp.Value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void ApplyDocumentCacheHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "0";
    }

    private static void ApplyStaticAssetCacheHeaders(HttpContext context, string fileName)
    {
        if (string.Equals(fileName, "index.html", StringComparison.OrdinalIgnoreCase))
        {
            ApplyDocumentCacheHeaders(context.Response);

            return;
        }

        var cacheControl = IsImmutableUiAsset(fileName)
                               ? "public, max-age=31536000, immutable"
                               : "public, max-age=600";

        context.Response.Headers.CacheControl = cacheControl;
    }

    private bool ConfigureUiHosting(WebApplication app)
    {
        ConfigureWebRootHosting(app);

        if (!_isUiEnabled)
        {
            return false;
        }

        var uiDistPath = ResolveUiDistPath(_uiDistPath);

        if (uiDistPath is null)
        {
            Log.Debug("UI hosting enabled but no UI dist directory found. Checked common locations.");

            return false;
        }

        var fileProvider = new PhysicalFileProvider(uiDistPath);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(
            new StaticFileOptions
            {
                FileProvider = fileProvider,
                OnPrepareResponse = context => ApplyStaticAssetCacheHeaders(context.Context, context.File.Name)
            }
        );

        var indexPath = Path.Combine(uiDistPath, "index.html");
        app.MapFallback(
            async context =>
            {
                if (!HttpMethods.IsGet(context.Request.Method) || ShouldSkipSpaFallback(context.Request.Path))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;

                    return;
                }

                ApplyDocumentCacheHeaders(context.Response);
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(indexPath);
            }
        );

        Log.Information("Serving UI static files from {UiDistPath}", uiDistPath);

        return true;
    }

    private void ConfigureWebRootHosting(WebApplication app)
    {
        var webRootPath = _directoriesConfig[DirectoryType.WebRoot];

        if (!Directory.Exists(webRootPath))
        {
            return;
        }

        var fileProvider = new PhysicalFileProvider(webRootPath);
        app.UseStaticFiles(
            new StaticFileOptions
            {
                FileProvider = fileProvider,
                OnPrepareResponse = context => ApplyStaticAssetCacheHeaders(context.Context, context.File.Name)
            }
        );
    }

    private static Logger CreateHttpLogger(string logPath, LogEventLevel minimumLogLevel)
        => new LoggerConfiguration()
           .MinimumLevel
           .Is(minimumLogLevel)
           .Enrich
           .FromLogContext()
           .WriteTo
           .File(logPath, rollingInterval: RollingInterval.Day)
           .CreateLogger();

    private static string CreateLogPath(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        return Path.Combine(logDirectory, "moongate_http-.log");
    }

    private static string EscapeLabelValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
               .Replace("\\", "\\\\")
               .Replace("\"", "\\\"")
               .Replace("\n", "\\n");
    }

    private static string GetPrometheusTypeName(MetricType metricType)
        => metricType switch
        {
            MetricType.Counter   => "counter",
            MetricType.Gauge     => "gauge",
            MetricType.Histogram => "histogram",
            _                    => "untyped"
        };

    private static bool IsImmutableUiAsset(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".woff", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".woff2", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLabelName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var buffer = new char[value.Length];

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            buffer[i] = i == 0 && !char.IsLetter(c) || !char.IsLetterOrDigit(c) && c != '_' ? '_' : char.ToLowerInvariant(c);
        }

        return new(buffer);
    }

    private static string NormalizeMetricName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var buffer = new char[value.Length];

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            buffer[i] = char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_';
        }

        return new(buffer);
    }

    private static string? NormalizePublicAssetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Replace('\\', '/').Trim();

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }

    private static string? ResolveUiDistPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var explicitPath = Path.GetFullPath(configuredPath);

            if (Directory.Exists(explicitPath))
            {
                return explicitPath;
            }
        }

        var envPath = Environment.GetEnvironmentVariable("MOONGATE_UI_DIST");

        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var resolvedEnvPath = Path.GetFullPath(envPath);

            if (Directory.Exists(resolvedEnvPath))
            {
                return resolvedEnvPath;
            }
        }

        var currentDirPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "ui", "dist"));

        if (Directory.Exists(currentDirPath))
        {
            return currentDirPath;
        }

        var baseDirPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "ui", "dist"));

        if (Directory.Exists(baseDirPath))
        {
            return baseDirPath;
        }

        return null;
    }

    private static bool ShouldSkipSpaFallback(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/auth", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase);
    }
}
