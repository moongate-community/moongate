using System.Net;
using Moongate.Api.Registry;
using Moongate.Api.Server;
using Moongate.Core.Directories;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api.Internal;
using Serilog;

namespace Moongate.Server.Services.Api;

/// <summary>Starts the configured API listener after plugin registration and drains it before game services stop.</summary>
public sealed class ApiServerService : IApiServerService, IAsyncDisposable
{
    public const int StartupPriority = 110;

    private readonly ApiConfig _config;
    private readonly DirectoriesConfig _directories;
    private readonly ApiRegistry _registry;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger = Log.ForContext<ApiServerService>();
    private readonly BootstrapLifecycleTasks _lifecycle = new();
    private readonly Lock _lifecycleGate = new();
    private ApiServer? _server;
    private bool _stopping;

    /// <inheritdoc />
    public IPEndPoint? Endpoint => _server?.Endpoint;

    public ApiServerService(ApiConfig config, DirectoriesConfig directories, ApiRegistry registry, TimeProvider clock)
    {
        _config = config;
        _directories = directories;
        _registry = registry;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_lifecycleGate)
        {
            if (_stopping) { throw new InvalidOperationException("The API server cannot start after shutdown begins."); }
            return _lifecycle.StartAsync(StartCoreAsync);
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_lifecycleGate)
        {
            _stopping = true;
            return _lifecycle.StopAsync(StopCoreAsync);
        }
    }

    private async Task StartCoreAsync()
    {
        _config.Validate();
        if (!_config.Enabled)
        {
            if (_config.AutoGenerateCertificate)
            {
                using var certificate = new ApiCertificateStore().Load(_config, _directories, _clock);
            }
            _logger.Warning("API server is disabled; set api.enabled = true and configure mutual TLS certificates to enable it");
            return;
        }
        _server = ApiServerFactory.Create(_config, _directories, _registry, _clock);
        await _server.StartAsync().ConfigureAwait(false);
    }

    private async Task StopCoreAsync(Task? startup)
    {
        if (startup is not null) { await startup.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); }
        if (_server is not null) { await _server.DisposeAsync().ConfigureAwait(false); }
    }

    /// <summary>Stops admission and disposes the owned listener within its bounded shutdown budget.</summary>
    public ValueTask DisposeAsync()
        => new(StopAsync());
}
