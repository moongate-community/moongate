using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Tests.TestSupport.Ultima.Gumps;

/// <summary>
///     Records the gumps opened and closed, so a test can answer or close them by hand.
/// </summary>
public sealed class RecordingGumpService : IGumpService
{
    public List<(GameSession Session, GumpInstance Gump)> Opened { get; } = [];

    public List<(GameSession Session, string Id)> Closed { get; } = [];

    public void Open(GameSession session, GumpInstance gump)
    {
        Opened.Add((session, gump));
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
