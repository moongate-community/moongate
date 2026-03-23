using Moongate.Server.Data.Session;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Version;

namespace Moongate.Server.Interfaces.Session;

/// <summary>
/// Exposes the gameplay session state needed by packet listeners.
/// </summary>
public interface IGameSession
{
    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    long SessionId { get; }

    /// <summary>
    /// Gets the negotiated client version, when known.
    /// </summary>
    ClientVersion? ClientVersion { get; }

    /// <summary>
    /// Gets or sets the authenticated account id associated with the session.
    /// </summary>
    Serial AccountId { get; set; }

    /// <summary>
    /// Gets or sets the authenticated account type for the session.
    /// </summary>
    AccountType AccountType { get; set; }

    /// <summary>
    /// Gets or sets the selected character id for the session.
    /// </summary>
    Serial CharacterId { get; set; }

    /// <summary>
    /// Gets or sets the runtime character entity for the session.
    /// </summary>
    UOMobileEntity? Character { get; set; }

    /// <summary>
    /// Gets or sets the cached account character list.
    /// </summary>
    List<UOMobileEntity>? AccountCharactersCache { get; set; }

    /// <summary>
    /// Gets or sets the ping sequence.
    /// </summary>
    byte PingSequence { get; set; }

    /// <summary>
    /// Gets or sets the movement sequence.
    /// </summary>
    byte MoveSequence { get; set; }

    /// <summary>
    /// Gets or sets the cached self notoriety byte.
    /// </summary>
    byte SelfNotoriety { get; set; }

    /// <summary>
    /// Gets or sets the next eligible movement time.
    /// </summary>
    long MoveTime { get; set; }

    /// <summary>
    /// Gets or sets the last mobile-position event timestamp.
    /// </summary>
    long LastMobilePositionEventTimestamp { get; set; }

    /// <summary>
    /// Gets or sets movement throttle credit.
    /// </summary>
    long MoveCredit { get; set; }

    /// <summary>
    /// Gets or sets whether the controlled character is mounted.
    /// </summary>
    bool IsMounted { get; set; }

    /// <summary>
    /// Gets or sets the client view range.
    /// </summary>
    byte ViewRange { get; set; }

    /// <summary>
    /// Gets or sets the client hardware info.
    /// </summary>
    ClientHardwareInfo? HardwareInfo { get; set; }
}
