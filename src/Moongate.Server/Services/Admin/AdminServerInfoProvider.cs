using System.Diagnostics;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;

namespace Moongate.Server.Services.Admin;

/// <summary>Exposes process identity without database or live-world dependencies.</summary>
public sealed class AdminServerInfoProvider : IAdminServerInfoProvider
{
    private readonly ServerMode _mode;
    private readonly string _instanceId;
    private readonly string? _realmId;
    private readonly DateTime _startedAt;

    public AdminServerInfoProvider(ServerMode mode, RealmInstance? realm = null)
    {
        _mode = mode;
        _instanceId = (realm?.InstanceId ?? Guid.NewGuid()).ToString("N");
        _realmId = realm?.Descriptor.RealmId;
        using var process = Process.GetCurrentProcess();
        _startedAt = process.StartTime.ToUniversalTime();
    }

    public AdminServerInfo GetSnapshot()
        => new(
            VersionUtils.GetVersion(typeof(AdminServerInfoProvider).Assembly),
            VersionUtils.GetCodename(typeof(AdminServerInfoProvider).Assembly),
            _mode,
            _instanceId,
            _realmId,
            DateTime.UtcNow - _startedAt
        );
}
