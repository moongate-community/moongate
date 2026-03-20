using MemoryPack;
using Moongate.Persistence.Types;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Journal record appended for every persisted mutation.
/// </summary>
[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class JournalEntry
{
    [MemoryPackOrder(0)]
    public long SequenceId { get; set; }

    [MemoryPackOrder(1)]
    public long TimestampUnixMilliseconds { get; set; }

    [MemoryPackOrder(2)]
    public ushort TypeId { get; set; }

    [MemoryPackOrder(3)]
    public JournalEntityOperationType Operation { get; set; }

    [MemoryPackOrder(4)]
    public byte[] Payload { get; set; } = [];
}
