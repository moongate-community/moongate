using Moongate.Persistence.Interfaces;
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
///     and removes it from <see cref="IMobileService" />, then saves the copy and publishes
///     <see cref="CharacterLeftWorldEvent" /> off the loop. Stopping waits for the saves still running.
/// </summary>
public sealed class CharacterLeaveWorldService : ISessionClosedListener, IMoongateStartupService
{
    private readonly ILogger _logger = Log.ForContext<CharacterLeaveWorldService>();
    private readonly Lock _gate = new();
    private readonly HashSet<Task> _pending = [];
    private readonly IMobileService _mobiles;
    private readonly IDataAccess<MobileEntity> _data;
    private readonly IMoongateEventBus _events;

    public CharacterLeaveWorldService(IMobileService mobiles, IDataAccess<MobileEntity> data, IMoongateEventBus events)
    {
        _mobiles = mobiles;
        _data = data;
        _events = events;
    }

    public void OnSessionClosed(GameSession session)
    {
        if (!session.CharacterId.IsValid || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            return;
        }

        var snapshot = character.Snapshot();
        _mobiles.LeaveWorld(character.Id);
        Track(Task.Run(() => SaveAndPublishAsync(snapshot)));
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

    private async Task SaveAndPublishAsync(MobileEntity character)
    {
        try
        {
            await _data.UpsertAsync(character, CancellationToken.None);
            _logger.Information("{Character} left the world", character);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Saving {Character} as it left the world failed", character);
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
