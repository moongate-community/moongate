using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.ContextMenus;

/// <summary>
///     Records the menus asked for and the choices made.
/// </summary>
public sealed class RecordingContextMenuService : IContextMenuService
{
    public List<(GameSession Session, Serial Target)> Requested { get; } = [];

    public List<(GameSession Session, Serial Target, int Index)> Selected { get; } = [];

    public bool Request(GameSession session, Serial target)
    {
        Requested.Add((session, target));

        return true;
    }

    public bool Select(GameSession session, Serial target, int index)
    {
        Selected.Add((session, target, index));

        return true;
    }
}
