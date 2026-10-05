using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Interfaces.Admin;

namespace Moongate.Tests.TestSupport.Admin;

/// <summary>
///     Gives the server info a test sets.
/// </summary>
public sealed class SettableServerInfoProvider : IAdminServerInfoProvider
{
    public AdminServerInfo Info { get; set; }

    public SettableServerInfoProvider(AdminServerInfo info)
    {
        Info = info;
    }

    public AdminServerInfo GetSnapshot()
    {
        return Info;
    }
}
