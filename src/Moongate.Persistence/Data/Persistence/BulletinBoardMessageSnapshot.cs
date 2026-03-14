using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class BulletinBoardMessageSnapshot
{
    public uint MessageId { get; set; }

    public uint BoardId { get; set; }

    public uint ParentId { get; set; }

    public uint OwnerCharacterId { get; set; }

    public string Author { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public long PostedAtUtcTicks { get; set; }

    public string[] BodyLines { get; set; } = [];
}
