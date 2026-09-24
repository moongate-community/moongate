using System.Net;

namespace Moongate.Server.Admin.Data.Config;

/// <summary>Configures the optional embedded administration endpoint.</summary>
public sealed class AdminApiConfig
{
    public bool Enabled { get; set; }
    /// <summary>Gets or sets a literal bind address, or "*" for all IPv4 interfaces.</summary>
    public string ListenAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 2590;
    public int SessionLifetimeMinutes { get; set; } = 30;
    public int MaxReceiveMessageBytes { get; set; } = 65536;
    public int MaxConcurrentCalls { get; set; } = 64;
    public bool AllowInsecureLoopback { get; set; }
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";

    /// <summary>Validates scalar settings without resolving paths or secrets.</summary>
    public void Validate()
    {
        var address = ResolveListenAddress();
        if (AllowInsecureLoopback && !IPAddress.IsLoopback(address))
        {
            throw new InvalidOperationException("admin_api.allow_insecure_loopback requires a loopback address.");
        }
        if (Port is < 1 or > 65535 || SessionLifetimeMinutes is < 1 or > 1440 ||
            MaxReceiveMessageBytes is < 1024 or > 1048576 || MaxConcurrentCalls is < 1 or > 1024)
        {
            throw new InvalidOperationException("admin_api numeric settings are outside their supported bounds.");
        }
    }

    internal IPAddress ResolveListenAddress()
    {
        if (ListenAddress == "*")
        {
            return IPAddress.Any;
        }
        if (!IPAddress.TryParse(ListenAddress, out var address))
        {
            throw new InvalidOperationException("admin_api.listen_address must be a literal IP address or '*'.");
        }
        return address;
    }
}
