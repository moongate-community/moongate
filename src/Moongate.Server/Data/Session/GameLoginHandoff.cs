using Moongate.UO.Data.Version;

namespace Moongate.Server.Data.Session;

/// <summary>
/// Carries client handshake metadata across the login-server to game-server reconnect.
/// </summary>
public sealed class GameLoginHandoff
{
    public GameLoginHandoff(uint sessionKey, ClientType clientType, ClientVersion? clientVersion, long createdAtUnixTimeMs)
    {
        SessionKey = sessionKey;
        ClientType = clientType;
        ClientVersion = clientVersion;
        CreatedAtUnixTimeMs = createdAtUnixTimeMs;
    }

    /// <summary>
    /// Gets the redirect session key used by the reconnecting client.
    /// </summary>
    public uint SessionKey { get; }

    /// <summary>
    /// Gets the resolved client type observed on the login socket.
    /// </summary>
    public ClientType ClientType { get; }

    /// <summary>
    /// Gets the negotiated client version observed on the login socket, when available.
    /// </summary>
    public ClientVersion? ClientVersion { get; }

    /// <summary>
    /// Gets the creation timestamp in Unix milliseconds.
    /// </summary>
    public long CreatedAtUnixTimeMs { get; }
}
