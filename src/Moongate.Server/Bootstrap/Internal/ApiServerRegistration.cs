using DryIoc;
using Moongate.Api.Registry;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api;

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
        return container.RegisterMoongateService<IApiServerService, ApiServerService>(ApiServerService.StartupPriority);
    }
}
