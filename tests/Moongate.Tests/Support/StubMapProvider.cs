using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Maps;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Support;

/// <summary>Test double for <see cref="IUltimaMapProvider" />: serves exactly one synthetic facet.</summary>
public sealed class StubMapProvider : IUltimaMapProvider
{
    private readonly MapType _facet;
    private readonly Map _map;

    public IReadOnlyList<MapType> Facets { get; }

    public StubMapProvider(MapType facet, Map map)
    {
        _facet = facet;
        _map = map;
        Facets = [facet];
    }

    public Map? Get(MapType facet)
        => facet == _facet ? _map : null;
}
