using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Moongates;

/// <summary>
///     Gives the facets it is handed, as they are.
/// </summary>
public sealed class StubPublicMoongateService : IPublicMoongateService
{
    public List<MoongateFacet> Facets { get; } = [];

    public IReadOnlyList<MoongateFacet> GetFacets()
    {
        return Facets;
    }
}
