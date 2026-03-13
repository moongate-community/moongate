using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Persistence.Entities;

public sealed class BulletinBoardMessageEntity
{
    public Serial MessageId { get; set; }

    public Serial BoardId { get; set; }

    public Serial ParentId { get; set; }

    public Serial OwnerCharacterId { get; set; }

    public string Author { get; set; }

    public string Subject { get; set; }

    public DateTime PostedAtUtc { get; set; }

    public List<string> BodyLines { get; } = [];
}
