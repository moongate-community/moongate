using Moongate.Server.Core.Types.Hosting;

namespace Moongate.Server.Core.Data.Admin;

/// <summary>
///     Non-sensitive process and realm identity snapshot.
/// </summary>
public sealed class AdminServerInfo
{
    public string Version { get; }
    public string Codename { get; }
    public ServerMode Mode { get; }
    public string InstanceId { get; }
    public string? RealmId { get; }
    public TimeSpan Uptime { get; }

    /// <summary>
    ///     Gets the configuration the server was built in, Debug or Release; empty when its build did not say.
    /// </summary>
    public string Configuration { get; }

    /// <summary>
    ///     Gets when the server was built, in UTC; null when its build did not say.
    /// </summary>
    public DateTimeOffset? BuiltAt { get; }

    public AdminServerInfo(
        string version,
        string codename,
        ServerMode mode,
        string instanceId,
        string? realmId,
        TimeSpan uptime,
        string configuration = "",
        DateTimeOffset? builtAt = null
    )
    {
        Version = version;
        Codename = codename;
        Mode = mode;
        InstanceId = instanceId;
        RealmId = realmId;
        Uptime = uptime;
        Configuration = configuration;
        BuiltAt = builtAt;
    }
}
