using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moongate.Core.Directories;
using Moongate.Core.Extensions.Directories;
using Moongate.Core.Extensions.Env;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;
using Serilog;

namespace Moongate.Server.Admin.Services;

/// <summary>Hosts the optional embedded HTTP/2 endpoint without owning Moongate application services.</summary>
public sealed class AdminGrpcHostService : IAdminApiService, IAsyncDisposable
{
    public const int StartupPriority = 110;
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(15);
    private readonly Serilog.ILogger _logger = Log.ForContext<AdminGrpcHostService>();
    private readonly AdminApiConfig _config;
    private readonly DirectoriesConfig _directories;
    private readonly ServerMode _mode;
    private readonly Action<IServiceCollection> _configureServices;
    private readonly AdminRequestGate _gate;
    private WebApplication? _app;
    private X509Certificate2? _certificate;

    public AdminGrpcHostService(AdminApiConfig config, DirectoriesConfig directories, ServerMode mode,
        Action<IServiceCollection> configureServices)
    {
        _config = config;
        _directories = directories;
        _mode = mode;
        _configureServices = configureServices;
        _gate = new(config.MaxConcurrentCalls);
    }

    public async Task StartAsync()
    {
        if (_app is not null) { return; }
        _config.Validate();
        if (!_config.Enabled)
        {
            _logger.Information("Administration gRPC endpoint disabled");
            return;
        }
        if (!_config.AllowInsecureLoopback) { _certificate = LoadCertificate(); }
        else { _logger.Warning("Administration gRPC uses plaintext HTTP/2 on explicit loopback only"); }
        try
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
            builder.Logging.ClearProviders();
            builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = ShutdownTimeout);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = _config.MaxReceiveMessageBytes + 5L;
                options.Listen(IPAddress.Parse(_config.ListenAddress), _config.Port, listen =>
                {
                    listen.Protocols = HttpProtocols.Http2;
                    if (_certificate is not null) { listen.UseHttps(_certificate); }
                });
            });
            _configureServices(builder.Services);
            _app = builder.Build();
            AdminGrpcApplication.Configure(_app, _mode, _gate);
            await _app.StartAsync();
            _logger.Information("Administration gRPC listening on {Address}:{Port} for {Mode}; awaiting server readiness",
                _config.ListenAddress, _config.Port, _mode);
        }
        catch (Exception)
        {
            await StopAsync();
            throw new InvalidOperationException("Cannot start admin_api listener. Check bind address, port and service registration.");
        }
    }

    public void Activate() => _gate.Activate();
    public void StopAccepting() => _gate.StopAccepting();

    public async Task StopAsync()
    {
        StopAccepting();
        var app = _app;
        _app = null;
        try
        {
            if (app is not null)
            {
                using var deadline = new CancellationTokenSource(ShutdownTimeout);
                try { await app.StopAsync(deadline.Token); }
                finally { await app.DisposeAsync(); }
                _logger.Information("Administration gRPC endpoint stopped");
            }
        }
        finally
        {
            _certificate?.Dispose();
            _certificate = null;
        }
    }

    private X509Certificate2 LoadCertificate()
    {
        try
        {
            var template = _config.CertificatePath.ExpandEnvironmentVariables(true);
            if (string.IsNullOrWhiteSpace(template)) { throw new InvalidOperationException(); }
            var path = template.StartsWith('~') || Path.IsPathRooted(template)
                ? template.ResolvePathAndEnvs() : Path.Combine(_directories.Root, template).ResolvePathAndEnvs();
            var password = _config.CertificatePassword.ExpandEnvironmentVariables(true);
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
                certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
            {
                certificate.Dispose();
                throw new InvalidOperationException();
            }
            return certificate;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Invalid admin_api certificate. Configure a readable, unexpired server PFX with a private key and matching password.");
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
