namespace Moongate.Persistence.Data.Internal;

internal sealed class PersistenceHeader
{
    public ulong Sequence { get; }

    public ulong Count { get; }

    public PersistenceHeader(ulong sequence, ulong count)
    {
        Sequence = sequence;
        Count = count;
    }
}
