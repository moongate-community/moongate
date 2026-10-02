using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Gumps;
using Moongate.Server.Ultima.Types.Gumps;

namespace Moongate.Tests.TestSupport.Ultima.Gumps;

/// <summary>
///     Records the gumps opened and closed, so a test can answer or close them by hand. Opening a gump with the id of one
///     still open tells that one it was replaced, as <c>GumpService</c> does.
/// </summary>
public sealed class RecordingGumpService : IGumpService
{
    private readonly HashSet<GumpInstance> _replaced = [];

    public List<(GameSession Session, GumpInstance Gump)> Opened { get; } = [];

    public List<(GameSession Session, string Id)> Closed { get; } = [];

    public void Open(GameSession session, GumpInstance gump)
    {
        var replaced = Opened.LastOrDefault(open => open.Gump.Id == gump.Id && !_replaced.Contains(open.Gump)).Gump;
        Opened.Add((session, gump));

        if (replaced is not null)
        {
            _replaced.Add(replaced);
            replaced.OnClosed?.Invoke(session, GumpCloseReasonType.Replaced);
        }
    }

    public bool Close(GameSession session, string id)
    {
        Closed.Add((session, id));

        return true;
    }

    public void Respond(GameSession session, GumpResponsePacket packet)
    {
    }

    public void OnSessionClosed(GameSession session)
    {
    }
}
