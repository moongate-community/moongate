using System.Net;
using System.Security.Authentication;
using Moongate.Api.Exceptions;
using Moongate.Api.Interfaces.Client;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Types.Protocol;
using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Core.Types.Realms;
using Moongate.Server.Data.Config.Sections;
using Serilog;

namespace Moongate.Server.Services.Realms;

/// <summary>Keeps a ready game process listed on its login server.</summary>
public sealed class RealmRegistrationService : IMoongateStartupService, IAsyncDisposable
{
    public const int StartupPriority = 105;

    private readonly IApiClient _client;
    private readonly RealmDirectoryConfig _config;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger = Log.ForContext<RealmRegistrationService>();
    private readonly Lock _gate = new();
    private CancellationTokenSource? _shutdown;
    private Task? _loop;
    private Task? _stop;

    public RealmRegistrationService(IApiClient client, RealmDirectoryConfig config, TimeProvider clock)
    {
        _client = client;
        _config = config;
        _clock = clock;
    }

    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_shutdown is not null || _stop is not null)
            {
                throw new InvalidOperationException("Realm registration cannot be started twice.");
            }

            _config.Validate(ServerMode.Game);
            _shutdown = new();
            var instanceId = Guid.NewGuid();
            _loop = Task.Run(() => RunAsync(instanceId, _shutdown.Token));
            return Task.CompletedTask;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            return _stop ??= StopCoreAsync();
        }
    }

    private async Task RunAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var backoffSeconds = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(_config.LoginApiHost, cancellationToken)
                    .ConfigureAwait(false);
                var address = addresses.FirstOrDefault(value => value.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                              ?? addresses.FirstOrDefault()
                              ?? throw new IOException("Login API host has no address.");
                await using var connection = await _client.ConnectAsync(
                    new IPEndPoint(address, _config.LoginApiPort), _config.LoginApiHost,
                    _config.ExpectedLoginPeerId, cancellationToken).ConfigureAwait(false);
                backoffSeconds = 1;
                if (!await RegisterAndRenewAsync(connection, instanceId, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (AuthenticationException exception)
            {
                _logger.Error(exception, "Realm registration rejected by API TLS identity; check certificate trust and peer ID");
                return;
            }
            catch (ApiRemoteException exception) when (exception.Code is ApiErrorCode.Forbidden or ApiErrorCode.UnsupportedOperation)
            {
                _logger.Error(exception, "Realm registration API operation is not permitted");
                return;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or ApiRemoteException or
                                               System.Net.Sockets.SocketException)
            {
                _logger.Warning(exception, "Login API unavailable; retrying realm registration in {DelaySeconds} seconds",
                    backoffSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), _clock, cancellationToken).ConfigureAwait(false);
            backoffSeconds = Math.Min(backoffSeconds * 2, 10);
        }
    }

    private async Task<bool> RegisterAndRenewAsync(IApiConnection connection, Guid instanceId,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var response = await connection.RequestAsync<RegisterRealmRequest, RegisterRealmResponse>(new()
            {
                RealmId = _config.RealmId,
                InstanceId = instanceId.ToByteArray(),
                ServerIndex = checked((ushort)_config.ServerIndex),
                Name = _config.Name,
                AdvertisedAddress = _config.AdvertisedAddress,
                AdvertisedPort = checked((ushort)_config.AdvertisedPort),
                MinimumAccountType = _config.MinimumAccountType
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!response.Accepted)
            {
                _logger.Error("Realm {RealmId} registration rejected: {Error}", _config.RealmId, response.Error);
                return false;
            }

            if (response.LeaseId?.Length != 16)
            {
                throw new InvalidDataException("Login API returned an invalid realm lease ID.");
            }

            var lease = new Guid(response.LeaseId);
            _logger.Information("Realm {RealmId} registered with login API as index {ServerIndex}",
                _config.RealmId, _config.ServerIndex);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds), _clock, cancellationToken)
                        .ConfigureAwait(false);
                    var renewed = await connection.RequestAsync<RenewRealmRequest, RenewRealmResponse>(new()
                    {
                        RealmId = _config.RealmId,
                        LeaseId = lease.ToByteArray()
                    }, cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (renewed.Accepted)
                    {
                        continue;
                    }

                    if (renewed.Error == RealmRegistrationError.StaleLease)
                    {
                        _logger.Warning("Realm {RealmId} lease was superseded; stopping this registration instance",
                            _config.RealmId);
                        return false;
                    }

                    if (renewed.Error == RealmRegistrationError.ExpiredLease)
                    {
                        _logger.Warning("Realm {RealmId} lease expired; registering again", _config.RealmId);
                        break;
                    }

                    _logger.Error("Realm {RealmId} renewal rejected: {Error}", _config.RealmId, renewed.Error);
                    return false;
                }
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await connection.RequestAsync<UnregisterRealmRequest, UnregisterRealmResponse>(new()
                        {
                            RealmId = _config.RealmId,
                            LeaseId = lease.ToByteArray()
                        }, cancellationToken: timeout.Token).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is IOException or OperationCanceledException or ApiRemoteException)
                    {
                        _logger.Warning(exception, "Realm {RealmId} could not unregister before shutdown", _config.RealmId);
                    }
                }
            }
        }

        return true;
    }

    private async Task StopCoreAsync()
    {
        Task? loop;
        CancellationTokenSource? shutdown;
        lock (_gate)
        {
            loop = _loop;
            shutdown = _shutdown;
        }

        if (shutdown is not null)
        {
            await shutdown.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            if (loop is not null)
            {
                await loop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
        finally
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            shutdown?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
        => new(StopAsync());
}
