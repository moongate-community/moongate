using DryIoc;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Admin;

namespace Moongate.Server.Bootstrap.Internal;

internal static class AdminServiceRegistration
{
    public static void Register(Container container, MoongateServerConfig config)
    {
        // The Admin plugin adds AdminApiConfig when its Register runs, after this.
        container.RegisterDelegate(
            resolver => new AdminSessionOptions(
                TimeSpan.FromMinutes(resolver.Resolve<AdminApiConfig>().SessionLifetimeMinutes)
            ),
            Reuse.Singleton
        );
        container.Register<IAdminSessionStore, RedisAdminSessionStore>(Reuse.Singleton);
        container.Register<IAdminLoginThrottle, RedisAdminLoginThrottle>(Reuse.Singleton);
        container.RegisterDelegate<IAdminServerInfoProvider>(
            resolver => new AdminServerInfoProvider(
                config.Mode,
                (config.Mode & ServerMode.Game) != 0 ? resolver.Resolve<RealmInstance>() : null
            ),
            Reuse.Singleton
        );
    }
}
