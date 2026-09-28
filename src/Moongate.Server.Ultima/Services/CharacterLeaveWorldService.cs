using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Takes a player's character out of the world when its session closes: on the game loop it copies the character
///     and the items it carries and removes them from <see cref="IMobileService" /> and <see cref="IItemService" />,
///     then off the loop saves the character, then its items, and publishes <see cref="CharacterLeftWorldEvent" />.
///     Stopping waits for the saves still running.
/// </summary>
public sealed class CharacterLeaveWorldService : ISessionClosedListener, IMoongateStartupService
{
    private readonly ILogger _logger = Log.ForContext<CharacterLeaveWorldService>();
    private readonly Lock _gate = new();
    private readonly HashSet<Task> _pending = [];
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IWorldTransactionService _world;
    private readonly IMoongateEventBus _events;

    public CharacterLeaveWorldService(
        IMobileService mobiles,
        IItemService items,
        IWorldTransactionService world,
        IMoongateEventBus events
    )
    {
        _mobiles = mobiles;
        _items = items;
        _world = world;
        _events = events;
    }

    public void OnSessionClosed(GameSession session)
    {
        if (!session.CharacterId.IsValid || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            return;
        }

        var snapshot = character.Snapshot();
        var carried = _items.GetOwnedBy(character.Id);
        var items = carried.Select(item => item.Snapshot()).ToList();
        var merged = _items.TombstonesOf(character.Id);
        _items.Remove(carried.Select(item => item.Id));
        _mobiles.LeaveWorld(character.Id);
        Track(Task.Run(() => SaveAndPublishAsync(snapshot, items, merged)));
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task[] pending;

        lock (_gate)
        {
            pending = _pending.ToArray();
        }

        await Task.WhenAll(pending);
    }

    private async Task SaveAndPublishAsync(
        MobileEntity character,
        IReadOnlyList<ItemEntity> items,
        IReadOnlyCollection<Serial> merged
    )
    {
        // One transaction: logging back in before the next world save must never find a merged stack again.
        try
        {
            await _world.ExecuteAsync(
                async transaction =>
                {
                    await transaction.GetDataAccess<MobileEntity>().UpsertAsync(character);
                    var data = transaction.GetDataAccess<ItemEntity>();

                    // After the character: the worn items point at its row.
                    foreach (var item in items)
                    {
                        await data.UpsertAsync(item);
                    }

                    foreach (var serial in merged)
                    {
                        await data.DeleteAsync(serial);
                    }
                }
            );
            _items.Committed(merged);
            _logger.Information("{Character} left the world", character);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Saving {Character} and its items as it left the world failed", character);
        }

        try
        {
            await _events.PublishAsync(new CharacterLeftWorldEvent(character), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Publishing that {Character} left the world failed", character);
        }
    }

    private void Track(Task task)
    {
        lock (_gate)
        {
            _pending.Add(task);
        }

        _ = task.ContinueWith(
            completed =>
            {
                lock (_gate)
                {
                    _pending.Remove(completed);
                }
            },
            TaskScheduler.Default
        );
    }
}
