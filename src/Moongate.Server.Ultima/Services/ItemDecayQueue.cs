using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     A queue of ground items by decay time, as a simplified ModernUO decay scheduler. An item decays when it is movable
///     and its template decays and players can see it. An entry counts only while the item's
///     <see cref="ItemEntity.DecayAt" /> still equals its time, so restarting or stopping an item needs no removal.
/// </summary>
public sealed class ItemDecayQueue : IItemDecayQueue
{
    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly TimeProvider _time;
    private readonly PriorityQueue<ItemEntity, DateTime> _queue = new();

    public ItemDecayQueue(IItemTemplateService templates, ITileDataService tiles, TimeProvider time)
    {
        _templates = templates;
        _tiles = tiles;
        _time = time;
    }

    public void Restart(ItemEntity item)
    {
        item.DecayAt = DecayTime(item) is { } lasts ? _time.GetUtcNow().UtcDateTime + lasts : null;
        Enqueue(item);
    }

    public void Track(ItemEntity item)
    {
        if (item.DecayAt is null || DecayTime(item) is null)
        {
            Restart(item);

            return;
        }

        Enqueue(item);
    }

    public void Stop(ItemEntity item)
    {
        item.DecayAt = null;
    }

    public IReadOnlyList<ItemEntity> TakeDue()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var due = new List<ItemEntity>();

        while (_queue.TryPeek(out var item, out var at) && at <= now)
        {
            _queue.Dequeue();

            if (item.DecayAt == at)
            {
                due.Add(item);
            }
        }

        return due;
    }

    private void Enqueue(ItemEntity item)
    {
        if (item.DecayAt is { } at)
        {
            _queue.Enqueue(item, at);
        }
    }

    // How long the item lasts on the ground, or null when it never decays.
    private TimeSpan? DecayTime(ItemEntity item)
    {
        if (!_templates.TryGet(item.TemplateId, out var template) ||
            !(item.Movable ?? template.EffectiveMovable(_tiles)) ||
            template.Visibility is { } visibility && visibility > AccountType.Regular ||
            !template.EffectiveDecays(_tiles))
        {
            return null;
        }

        return template.EffectiveDecayTime();
    }
}
