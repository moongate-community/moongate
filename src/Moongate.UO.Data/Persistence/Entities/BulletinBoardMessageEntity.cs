using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Persistence.Entities;

public sealed partial class BulletinBoardMessageEntity
{
    [MoongatePersistedMember(0)]
    public Serial MessageId { get; set; }

    [MoongatePersistedMember(1)]
    public Serial BoardId { get; set; }

    [MoongatePersistedMember(2)]
    public Serial ParentId { get; set; }

    [MoongatePersistedMember(3)]
    public Serial OwnerCharacterId { get; set; }

    [MoongatePersistedMember(4)]
    public string Author { get; set; }

    [MoongatePersistedMember(5)]
    public string Subject { get; set; }

    [MoongatePersistedMember(6)]
    public DateTime PostedAtUtc { get; set; }

    public List<string> BodyLines { get; } = [];
}
