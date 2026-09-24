using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config.Sections;
using Serilog;

namespace Moongate.Server.Services.Realms;

/// <summary>Keeps this game process's Redis realm lease live until shutdown or replacement.</summary>
public sealed class RedisRealmRegistrationService : IMoongateStartupService, IAsyncDisposable
{
    public const int StartupPriority = 105;

    private readonly IRealmPresenceService _presence;
    private readonly RealmInstance _realm;
    private readonly RealmDirectoryConfig _config;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger = Log.ForContext<RedisRealmRegistrationService>();
    private CancellationTokenSource? _shutdown;
    private Task? _loop;
    private Task? _stop;

    public RedisRealmRegistrationService(
        IRealmPresenceService presence,
        RealmInstance realm,
        RealmDirectoryConfig config,
        TimeProvider clock
    )
    {
        _presence = presence;
        _realm = realm;
        _config = config;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        if (_shutdown is not null)
        {
            throw new InvalidOperationException("Realm registration cannot be started twice.");
        }

        await _presence.RegisterAsync(_realm).ConfigureAwait(false);
        _logger.Information(
            "Realm {RealmId} registered in Redis as index {ServerIndex}",
            _realm.Descriptor.RealmId,
            _realm.Descriptor.ServerIndex
        );
        _shutdown = new();
        _loop = Task.Run(() => RenewLoopAsync(_shutdown.Token));
    }

    /// <inheritdoc />
    public Task StopAsync()
        => _stop ??= StopCoreAsync();

    private async Task RenewLoopAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds);
        var retry = TimeSpan.FromSeconds(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, _clock, cancellationToken).ConfigureAwait(false);

                if (!await _presence.RenewAsync(_realm, cancellationToken).ConfigureAwait(false))
                {
                    _logger.Warning(
                        "Realm {RealmId} was replaced by another process; lease renewal stopped",
                        _realm.Descriptor.RealmId
                    );

                    return;
                }

                retry = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.Warning(
                    exception,
                    "Realm {RealmId} Redis lease renewal failed; retrying",
                    _realm.Descriptor.RealmId
                );
                await Task.Delay(retry, _clock, cancellationToken).ConfigureAwait(false);
                retry = TimeSpan.FromSeconds(Math.Min(retry.TotalSeconds * 2, 10));
            }
        }
    }

    private async Task StopCoreAsync()
    {
        if (_shutdown is null)
        {
            return;
        }

        await _shutdown.CancelAsync().ConfigureAwait(false);

        if (_loop is not null)
        {
            await _loop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _presence.UnregisterAsync(_realm, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                exception,
                "Realm {RealmId} could not remove its Redis lease at shutdown",
                _realm.Descriptor.RealmId
            );
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await StopAsync().ConfigureAwait(false);
}
