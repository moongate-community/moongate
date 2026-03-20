using MemoryPack;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class BulletinBoardMessageEntity
{
    [MemoryPackOrder(0)]
    public Serial MessageId { get; set; }
    [MemoryPackOrder(1)]
    public Serial BoardId { get; set; }
    [MemoryPackOrder(2)]
    public Serial ParentId { get; set; }
    [MemoryPackOrder(3)]
    public Serial OwnerCharacterId { get; set; }
    [MemoryPackOrder(4)]
    public string Author { get; set; }
    [MemoryPackOrder(5)]
    public string Subject { get; set; }
    [MemoryPackOrder(6)]
    public DateTime PostedAtUtc { get; set; }

    [MemoryPackOrder(7)]
    public List<string> BodyLines { get; set; } = [];
}
