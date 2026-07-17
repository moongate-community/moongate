using Moongate.Network.Data;

namespace Moongate.Server.Abstractions.Data.World;

/// <summary>The built property list of one object: its content hash and the cliloc lines.</summary>
public sealed record OplSnapshot(int Hash, IReadOnlyList<OplEntry> Entries)
{
    public bool HasEntries => Entries.Count > 0;
}
