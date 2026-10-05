using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs the timers the items keep: one repeating <c>item_timers</c> timer every second takes the due ones from the
///     queue and calls <c>on_timer(serial, name)</c> of each item's script. A timer is the prop
///     <c>timer.&lt;name&gt;</c> of its item, so it is saved with it and comes back after a restart; one that came due
///     while the server was down, or while its owner was offline, runs as soon as the item is in the world again.
/// </summary>
public sealed class ItemTimerService : IItemTimerService, IMoongateStartupService
{
    public const string TimerName = "item_timers";
    public const string TimerFunction = "on_timer";
    public const int MaximumNameLength = 32;

    public static readonly TimeSpan MaximumDelay = TimeSpan.FromDays(365);

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger = Log.ForContext<ItemTimerService>();
    private readonly ITimerService _timers;
    private readonly IItemTimerQueue _queue;
    private readonly IItemService _items;
    private readonly IItemScriptService _scripts;
    private readonly TimeProvider _time;
    private readonly IInventoryMutationGuard? _inventory;

    private string? _timerId;

    public ItemTimerService(
        ITimerService timers,
        IItemTimerQueue queue,
        IItemService items,
        IItemScriptService scripts,
        TimeProvider time,
        IInventoryMutationGuard? inventory = null
    )
    {
        _inventory = inventory;
        _timers = timers;
        _queue = queue;
        _items = items;
        _scripts = scripts;
        _time = time;
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

    public bool Start(ItemEntity item, string name, TimeSpan delay)
    {
        if (_inventory?.Allows(item) == false || string.IsNullOrWhiteSpace(name) || name.Length > MaximumNameLength || delay <= TimeSpan.Zero || delay > MaximumDelay)
        {
            return false;
        }

        var dueAt = _time.GetUtcNow().ToUnixTimeMilliseconds() + (long)delay.TotalMilliseconds;
        item.SetProp(ItemTimerQueue.PropPrefix + name, dueAt);
        _queue.Schedule(item.Id, name, dueAt);

        return true;
    }

    public bool Stop(ItemEntity item, string name)
    {
        return _inventory?.Allows(item) != false && !string.IsNullOrWhiteSpace(name) && item.RemoveProp(ItemTimerQueue.PropPrefix + name);
    }

    public TimeSpan? Remaining(ItemEntity item, string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            item.Props?.GetValueOrDefault(ItemTimerQueue.PropPrefix + name) is not long dueAt)
        {
            return null;
        }

        var left = dueAt - _time.GetUtcNow().ToUnixTimeMilliseconds();

        return TimeSpan.FromMilliseconds(Math.Max(0, left));
    }

    // A timer callback that throws closes the timer wheel: one bad item must not stop the server.
    private void Check()
    {
        foreach (var entry in _queue.TakeDue())
        {
            try
            {
                var key = ItemTimerQueue.PropPrefix + entry.Name;

                // Stale: the item is gone, or its timer was stopped or started again since.
                if (!_items.TryGet(entry.Item, out var item) ||
                    item.Props?.GetValueOrDefault(key) is not long dueAt ||
                    dueAt != entry.DueAt)
                {
                    continue;
                }

                if (_inventory?.Allows(item) == false)
                {
                    _queue.Schedule(entry.Item, entry.Name, entry.DueAt);
                    continue;
                }

                // Gone before the script runs: the script may start it again.
                item.RemoveProp(key);
                var result = _scripts.Run(item, TimerFunction, entry.Name);

                if (result.Kind is ScriptResultKind.Missing or ScriptResultKind.Failed)
                {
                    _logger.Warning(
                        "The timer {Name} of item {Item} came due and nothing ran it: {Kind}",
                        entry.Name,
                        item,
                        result.Kind
                    );
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The timer {Name} of item {Serial} failed", entry.Name, entry.Item);
            }
        }
    }
}
