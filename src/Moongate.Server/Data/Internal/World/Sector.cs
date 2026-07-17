using Moongate.Core.Primitives;

namespace Moongate.Server.Data.Internal.World;

/// <summary>One 16×16-tile bucket of the spatial grid: the serials living in it, split by kind.</summary>
internal sealed class Sector
{
    public HashSet<Serial> Mobiles { get; } = [];

    public HashSet<Serial> Items { get; } = [];

    public bool IsEmpty
        => Mobiles.Count == 0 && Items.Count == 0;
}
