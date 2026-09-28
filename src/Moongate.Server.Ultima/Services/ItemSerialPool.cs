using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps up to <see cref="Capacity" /> item serials reserved from the items sequence. Taking one never blocks; when
///     fewer than <see cref="RefillBelow" /> are left, one refill runs off the loop. A failed refill is logged and runs
///     again on the next take.
/// </summary>
public sealed class ItemSerialPool : IItemSerialPool
{
    public const int Capacity = 64;
    public const int RefillBelow = 16;

    private readonly ILogger _logger = Log.ForContext<ItemSerialPool>();
    private readonly ConcurrentQueue<Serial> _serials = new();
    private readonly Lock _gate = new();
    private readonly IDataAccess<ItemEntity> _items;
    private Task _refill = Task.CompletedTask;

    public ItemSerialPool(IDataAccess<ItemEntity> items)
    {
        _items = items;
    }

    public Task StartAsync()
    {
        return StartRefill();
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            return _refill;
        }
    }

    public bool TryTake(out Serial serial)
    {
        var taken = _serials.TryDequeue(out serial);

        if (_serials.Count < RefillBelow)
        {
            StartRefill();
        }

        return taken;
    }

    private Task StartRefill()
    {
        lock (_gate)
        {
            if (!_refill.IsCompleted)
            {
                return _refill;
            }

            _refill = Task.Run(RefillAsync);

            return _refill;
        }
    }

    private async Task RefillAsync()
    {
        try
        {
            while (_serials.Count < Capacity)
            {
                _serials.Enqueue(await _items.ReserveSerialAsync());
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Reserving item serials failed; the pool has {Count} left", _serials.Count);
        }
    }
}
