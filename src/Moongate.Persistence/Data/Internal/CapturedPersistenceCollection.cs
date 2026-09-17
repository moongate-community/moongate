using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces.Internal;

namespace Moongate.Persistence.Data.Internal;

internal sealed class CapturedPersistenceCollection
{
    public IPersistenceCollection Collection { get; }

    public Dictionary<Serial, byte[]> Payloads { get; }

    public CapturedPersistenceCollection(
        IPersistenceCollection collection,
        Dictionary<Serial, byte[]> payloads
    )
    {
        Collection = collection;
        Payloads = payloads;
    }
}
