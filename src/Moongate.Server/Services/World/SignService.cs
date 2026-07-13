using Moongate.Server.Interfaces;
using Moongate.UO.Data.Signs;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory registry of world sign placements. Populated at startup by
/// <see cref="Moongate.Server.Loaders.SignsLoader" />. Signs are not uniquely keyed, so they are held
/// as an ordered list and queried by map.
/// </summary>
public sealed class SignService : ISignService
{
    private readonly List<SignEntry> _signs = [];

    public IReadOnlyList<SignEntry> All => _signs;

    public int Count => _signs.Count;

    public void Register(SignEntry sign)
    {
        _signs.Add(sign);
    }

    public IReadOnlyList<SignEntry> ForMap(MapType map)
    {
        return [.. _signs.Where(sign => sign.Map == map)];
    }
}
