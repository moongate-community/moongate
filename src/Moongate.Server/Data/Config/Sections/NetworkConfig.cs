using System.Net;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Network;

namespace Moongate.Server.Data.Config.Sections;

public class NetworkConfig
{
    public NetworkEncryptionConfig Encryption { get; set; } = new();

    public int LoginPort { get; set; } = 2593;

    public int GamePort { get; set; } = 2595;

    public string ListenAddress { get; set; } = "0.0.0.0";

    public bool EnablePingServer { get; set; } = true;

    public int PingPort { get; set; } = 12000;

    /// <summary>
    ///     Gets the addresses the listeners bind: every local address for <c>0.0.0.0</c>, otherwise the configured one.
    /// </summary>
    public IPAddress[] ResolveListenAddresses()
    {
        return ListenAddress == "0.0.0.0" ? NetworkUtils.GetLocalIpAddresses().ToArray() : [IPAddress.Parse(ListenAddress)];
    }

    /// <summary>
    ///     Gets the options of the UDP ping server: one endpoint per listen address on <see cref="PingPort" />.
    /// </summary>
    public PingServerOptions ToPingServerOptions()
    {
        return new()
        {
            Enabled = EnablePingServer,
            Endpoints = EnablePingServer
                            ? ResolveListenAddresses().Select(address => new IPEndPoint(address, PingPort)).ToArray()
                            : []
        };
    }

    public void Validate(ServerMode mode)
    {
        if (Encryption is null)
        {
            throw new InvalidOperationException("network.encryption cannot be null.");
        }

        Encryption.Validate();

        if ((mode & ServerMode.Login) != 0 && LoginPort is < 0 or > 65535)
        {
            throw new InvalidOperationException("network.login_port must be between 0 and 65535.");
        }

        if ((mode & ServerMode.Game) != 0 && GamePort is < 0 or > 65535)
        {
            throw new InvalidOperationException("network.game_port must be between 0 and 65535.");
        }

        if (EnablePingServer && PingPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("network.ping_port must be between 1 and 65535.");
        }

        if (mode == ServerMode.Standalone && LoginPort == GamePort && LoginPort != 0)
        {
            throw new InvalidOperationException("network.login_port and network.game_port must differ in standalone mode.");
        }
    }
}
