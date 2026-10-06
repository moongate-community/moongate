using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Decorations;

/// <summary>
///     Gives the doors it was told a map calls for, one chunk per door; a map without doors has no chunks.
/// </summary>
public sealed class StubDoorGeneratorService : IDoorGeneratorService
{
    private readonly Dictionary<MapType, List<GeneratedDoor>> _doors = new();

    public StubDoorGeneratorService With(MapType map, params GeneratedDoor[] doors)
    {
        _doors[map] = [.. doors];

        return this;
    }

    public IReadOnlyList<Rectangle2D> ChunksOf(MapType map)
    {
        return _doors.TryGetValue(map, out var doors)
            ? doors.Select((_, index) => new Rectangle2D(index, 0, 1, 1)).ToList()
            : [];
    }

    public IReadOnlyList<GeneratedDoor> Scan(MapType map, Rectangle2D chunk)
    {
        return [_doors[map][chunk.X]];
    }
}
