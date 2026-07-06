namespace Moongate.Server.Data.Config;

/// <summary>Network binding and the address advertised to clients.</summary>
public sealed class MoongateNetworkConfig
{
    /// <summary>Local bind address for the TCP listener.</summary>
    public string Address { get; set; } = "0.0.0.0";

    /// <summary>TCP port for both login and game traffic (single process).</summary>
    public int Port { get; set; } = 2593;

    /// <summary>Address advertised to clients in the server list and game-server redirect.</summary>
    public string PublicAddress { get; set; } = "127.0.0.1";
}
