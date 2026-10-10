using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.ContextMenus;

/// <summary>
///     Records what was used as by a double click, and says which bodies have a paperdoll.
/// </summary>
public sealed class RecordingUseService : IUseService
{
    public List<(GameSession Session, Serial Target)> Used { get; } = [];

    /// <summary>
    ///     The bodies that have a paperdoll.
    /// </summary>
    public HashSet<int> PaperdollBodies { get; } = [400, 401];

    public void Use(GameSession session, Serial target)
    {
        Used.Add((session, target));
    }

    public List<(GameSession Session, Serial Target)> UsedFromAfar { get; } = [];

    public bool CanUseFromAfar(MobileEntity user, ItemEntity item)
    {
        return true;
    }

    public bool UseFromAfar(GameSession session, Serial target)
    {
        UsedFromAfar.Add((session, target));

        return true;
    }

    public bool HasPaperdoll(MobileEntity mobile)
    {
        return PaperdollBodies.Contains(mobile.Body);
    }
}
