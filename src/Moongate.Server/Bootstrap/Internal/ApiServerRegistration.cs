using DryIoc;
using Moongate.Api.Registry;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api;
using Moongate.Server.Extensions;
using Moongate.Server.Services.Realms.Api;

namespace Moongate.Server.Bootstrap.Internal;

internal static class ApiServerRegistration
{
    public static Container Register(Container container)
    {
        if (!container.IsRegistered<ApiRegistry>())
        {
            container.RegisterInstance(new ApiRegistry());
        }

        container.RegisterDelegate<ApiConfig>(resolver => resolver.Resolve<MoongateServerConfig>().Api, Reuse.Singleton);

        if (container.IsRegistered<MoongateServerConfig>() &&
            container.Resolve<MoongateServerConfig>().Mode == ServerMode.Login)
        {
            container.RegisterApiHandler<RegisterRealmHandler>();
            container.RegisterApiHandler<RenewRealmHandler>();
            container.RegisterApiHandler<UnregisterRealmHandler>();
        }

        return container.AddMoongateService<IApiServerService, ApiServerService>(ApiServerService.StartupPriority);
    }
}
