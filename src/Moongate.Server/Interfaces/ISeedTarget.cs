using Moongate.Server.Types;

namespace Moongate.Server.Interfaces;

/// <summary>The seed-handshake's minimal view of a session, so it can be unit-tested without a socket.</summary>
public interface ISeedTarget
{
    SessionStateType State { get; }

    void SetState(SessionStateType state);

    void SetSeed(uint seed);
}
