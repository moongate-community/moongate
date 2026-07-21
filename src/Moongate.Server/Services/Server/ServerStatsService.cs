using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Stats;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.Server;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Server;

/// <summary>
/// Counts what the shard holds on a repeating game-loop timer and publishes the result as an immutable
/// snapshot, so readers off the loop — the REST API — never touch the world stores themselves.
/// </summary>
public sealed class ServerStatsService : IServerStatsService, ISquidStdService
{
    private const string TimerName = "server-stats";

    private readonly ILogger _logger = Log.ForContext<ServerStatsService>();
    private readonly IEntityStore<AccountEntity, Serial> _accounts;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IEntityStore<ItemEntity, Serial> _items;
    private readonly ISessionManager _sessions;
    private readonly IItemTemplateService _itemTemplates;
    private readonly IMobileTemplateService _mobileTemplates;
    private readonly IGameLoopContext _loop;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _interval;
    private readonly DateTimeOffset _startedAt;

    private volatile ServerStatsSnapshot _current = ServerStatsSnapshot.Empty;
    private string? _timerId;

    public ServerStatsService(
        IPersistenceService persistenceService,
        ISessionManager sessions,
        IItemTemplateService itemTemplates,
        IMobileTemplateService mobileTemplates,
        IGameLoopContext loop,
        IEventBus eventBus,
        TimeProvider timeProvider,
        MoongateConfig config
    )
    {
        _accounts = persistenceService.GetStore<AccountEntity, Serial>();
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _items = persistenceService.GetStore<ItemEntity, Serial>();
        _sessions = sessions;
        _itemTemplates = itemTemplates;
        _mobileTemplates = mobileTemplates;
        _loop = loop;
        _eventBus = eventBus;
        _timeProvider = timeProvider;
        _interval = TimeSpan.FromSeconds(config.StatsRefreshSeconds);
        _startedAt = timeProvider.GetUtcNow();
    }

    public ServerStatsSnapshot Current => _current;

    public void Refresh()
    {
        try
        {
            var now = _timeProvider.GetUtcNow();
            var accounts = _accounts.GetAll();
            var characterIds = accounts.SelectMany(account => account.MobileIds).ToList();

            _current = new(
                now,
                now - _startedAt,
                characterIds.Count(_sessions.IsCharacterPlayed),
                _sessions.Count,
                accounts.Count,
                accounts.Count(account => account.IsActive),
                characterIds.Count,

                // Mobiles nobody owns are the NPCs. Floored, because an account can outlive the mobiles
                // its character ids point at.
                Math.Max(0, _mobiles.Count() - characterIds.Count),
                _items.Count(),
                _itemTemplates.Count,
                _mobileTemplates.Count
            );
        }
        catch (Exception exception)
        {
            // Keep the previous snapshot. This runs inside a game-loop timer callback: a stale number is
            // cheap, an exception escaping into the loop is not.
            _logger.Warning(exception, "Server stats refresh failed; keeping the previous snapshot");
        }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        // World-ready, not the timer, produces the first snapshot: this service starts before the data
        // loaders do, so a refresh scheduled for the next tick would count zero templates and publish that
        // for a whole interval. The event fires on the loop once the world is loaded, which is both the
        // earliest correct moment and the right thread. Refreshing inline here is not an option — StartAsync
        // runs off the loop, and reading the world stores from there is what this snapshot exists to avoid.
        _eventBus.Subscribe<WorldReadyEvent>(
            (_, _) =>
            {
                Refresh();

                return Task.CompletedTask;
            }
        );

        _timerId = _loop.ScheduleRepeating(TimerName, _interval, Refresh);

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_timerId is not null)
        {
            _loop.Cancel(_timerId);
            _timerId = null;
        }

        return ValueTask.CompletedTask;
    }
}
