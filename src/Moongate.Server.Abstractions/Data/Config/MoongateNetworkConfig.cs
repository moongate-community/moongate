namespace Moongate.Server.Abstractions.Data.Config;

/// <summary>Network binding and the address advertised to clients.</summary>
public sealed class MoongateNetworkConfig
{
    /// <summary>Local bind address for the TCP listener.</summary>
    public string Address { get; set; } = "0.0.0.0";

    /// <summary>TCP port for both login and game traffic (single process).</summary>
    public int Port { get; set; } = 2593;

    /// <summary>Address advertised to clients in the server list and game-server redirect.</summary>
    public string PublicAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// How far ahead of schedule a step may arrive and still be taken, in milliseconds. Network jitter
    /// routinely delivers movement a few milliseconds early, and refusing those is what makes walking
    /// stutter: every refusal snaps the client back to the server's position. Arriving late rebuilds
    /// the slack, arriving early spends it, and past the matching debt limit a step waits its turn
    /// instead. ModernUO's figure, which it then extends by up to half the measured round-trip time
    /// for distant players; there are no latency probes here, so this is the whole allowance. 0
    /// restores the old behaviour of refusing anything early.
    /// </summary>
    public int MovementCreditMilliseconds { get; set; } = 200;

    /// <summary>
    /// How many early steps may wait at once before the client is resynced. The UO client keeps at
    /// most five movements unacknowledged, so a queue this deep already means something other than
    /// jitter. ModernUO uses the same limit.
    /// </summary>
    public int MovementQueueLimit { get; set; } = 10;
}
