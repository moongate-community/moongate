using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class TestAdminServerInfoProvider : IAdminServerInfoProvider
{
    public AdminServerInfo GetSnapshot() => new("0.6.0", "Lilly", ServerMode.Login, "test-instance", null, TimeSpan.FromSeconds(42));
}
