using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Internal.World;

/// <summary>
///     What the world view sent a player in one step, for the debug log of a sector entry.
/// </summary>
public sealed class SentCounts
{
    public int Items { get; set; }

    public int Npcs { get; private set; }

    public int Players { get; private set; }

    public void Add(MobileEntity mobile)
    {
        if (mobile.IsNpc)
        {
            Npcs++;
        }
        else
        {
            Players++;
        }
    }
}
