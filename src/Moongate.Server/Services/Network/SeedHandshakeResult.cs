namespace Moongate.Server.Services.Network;

/// <summary>Outcome of the initial seed handshake for a frame.</summary>
public enum SeedHandshakeResult : byte
{
    /// <summary>The frame (or its leading bytes) was the seed; continue with the remainder.</summary>
    Consumed = 0,

    /// <summary>Nothing to consume; dispatch the frame normally.</summary>
    PassThrough = 1,

    /// <summary>Malformed handshake; the caller must close the session.</summary>
    Reject = 2
}
