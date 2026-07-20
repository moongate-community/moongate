namespace Moongate.Console.Admin.Plugin.Data.Config;

/// <summary>Config for the admin console (section <c>console</c> in moongate.yaml). Opt-in.</summary>
public sealed class MoongateConsoleConfig
{
    /// <summary>When false the console never binds. Default false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Bind address. Default loopback — the console is plaintext, keep it off public NICs.</summary>
    public string Address { get; set; } = "127.0.0.1";

    /// <summary>Listen port. Default 4050. Use 0 to let the OS choose (tests).</summary>
    public int Port { get; set; } = 4050;

    /// <summary>Maximum concurrent sessions. Default 4.</summary>
    public int MaxSessions { get; set; } = 4;
}
