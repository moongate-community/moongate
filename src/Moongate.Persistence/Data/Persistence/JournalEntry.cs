using MessagePack;
using Moongate.Persistence.Types;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Journal record appended for every persisted mutation.
/// </summary>
[MessagePackObject(true)]
public sealed class JournalEntry
{
    public long SequenceId { get; set; }

    public long TimestampUnixMilliseconds { get; set; }

    public ushort TypeId { get; set; }

    public JournalEntityOperationType Operation { get; set; }

    public byte[] Payload { get; set; } = [];
}
