using Moongate.Core.Primitives;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Data.Internal;

internal sealed class PersistenceRecord
{
    public PersistenceOperation Operation { get; }

    public ulong Sequence { get; }

    public Serial Id { get; }

    public byte[] Payload { get; }

    public PersistenceRecord(PersistenceOperation operation, ulong sequence, Serial id, byte[] payload)
    {
        Operation = operation;
        Sequence = sequence;
        Id = id;
        Payload = payload;
    }
}
