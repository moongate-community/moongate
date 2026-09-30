using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Deletes the ground items whose decay time has passed: one repeating <c>item_decay</c> timer every 5 seconds takes
///     the due items from the decay queue, as ModernUO's decay scheduler. A decayed container takes its contents with it;
///     the world save deletes the rows. Decay does not pause when no player is near.
/// </summary>
public sealed class ItemDecayService : IMoongateStartupService
{
    public const string TimerName = "item_decay";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger _logger = Log.ForContext<ItemDecayService>();
    private readonly ITimerService _timers;
    private readonly IItemDecayQueue _queue;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;

    private string? _timerId;

    public ItemDecayService(ITimerService timers, IItemDecayQueue queue, IItemService items, IWorldViewService view)
    {
        _timers = timers;
        _queue = queue;
        _items = items;
        _view = view;
    }

    public Task StartAsync()
    {
        _timerId = _timers.RegisterTimer(TimerName, CheckInterval, Check, CheckInterval, true);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    // A timer callback that throws closes the timer wheel: one bad item must not stop the server.
    private void Check()
    {
        try
        {
            var decayed = 0;

            foreach (var item in _queue.TakeDue())
            {
                if (_items.TryGet(item.Id, out var live) && ReferenceEquals(live, item) && _items.IsLyingOnGround(item))
                {
                    Decay(item);
                    decayed++;
                }
            }

            if (decayed > 0)
            {
                _logger.Debug("{Count} ground items decayed", decayed);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Ground item decay failed");
        }
    }

    private void Decay(ItemEntity item)
    {
        _view.ItemDisappeared(item);
        AbsorbContents(item);
        _items.Absorb(item);
    }

    private void AbsorbContents(ItemEntity container)
    {
        foreach (var content in _items.GetContents(container.Id))
        {
            AbsorbContents(content);
            _items.Absorb(content);
        }
    }
}
