using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Data.Internal;

/// <summary>
/// In-memory mutable world state shared by persistence repositories.
/// </summary>
internal sealed class PersistenceStateStore
{
    private readonly Dictionary<ushort, object> _entityBuckets = [];

    public Dictionary<Serial, UOAccountEntity> AccountsById => GetBucket<UOAccountEntity, Serial>(PersistenceCoreEntityTypeIds.Account);

    public Dictionary<string, Serial> AccountNameIndex { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<Serial, UOMobileEntity> MobilesById => GetBucket<UOMobileEntity, Serial>(PersistenceCoreEntityTypeIds.Mobile);

    public Dictionary<Serial, UOItemEntity> ItemsById => GetBucket<UOItemEntity, Serial>(PersistenceCoreEntityTypeIds.Item);

    public Dictionary<Serial, BulletinBoardMessageEntity> BulletinBoardMessagesById =>
        GetBucket<BulletinBoardMessageEntity, Serial>(PersistenceCoreEntityTypeIds.BulletinBoardMessage);

    public Dictionary<Serial, HelpTicketEntity> HelpTicketsById =>
        GetBucket<HelpTicketEntity, Serial>(PersistenceCoreEntityTypeIds.HelpTicket);

    public object SyncRoot { get; } = new();

    public long LastSequenceId { get; set; }

    public uint LastAccountId { get; set; } = Serial.MobileStart - 1;

    public uint LastMobileId { get; set; } = Serial.MobileStart - 1;

    public uint LastItemId { get; set; } = Serial.ItemOffset - 1;

    public void ClearBuckets() => _entityBuckets.Clear();

    public Dictionary<TKey, TEntity> GetBucket<TEntity, TKey>(ushort typeId)
        where TKey : notnull
    {
        if (_entityBuckets.TryGetValue(typeId, out var existing))
        {
            return (Dictionary<TKey, TEntity>)existing;
        }

        var created = new Dictionary<TKey, TEntity>();
        _entityBuckets[typeId] = created;

        return created;
    }
}
