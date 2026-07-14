using Moongate.Server.Types;

namespace Moongate.Server.Interfaces.Network;

/// <summary>The seed-handshake's minimal view of a session, so it can be unit-tested without a socket.</summary>
public interface ISeedTarget
{
    SessionStateType State { get; }

    void SetSeed(uint seed);

    void SetState(SessionStateType state);
}
