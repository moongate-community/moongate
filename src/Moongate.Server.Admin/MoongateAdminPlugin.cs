using DryIoc;
using Moongate.Core.Directories;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Admin.Services;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Admin;

/// <summary>Registers the administration listener embedded in the server distribution.</summary>
public sealed class MoongateAdminPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata
        => new(
            "com.github.moongate-community.moongate.plugins.admin",
            "Moongate Administration",
            new(1, 0),
            "squid",
            "Optional gRPC account and server administration.",
            [new("com.github.moongate-community.moongate.plugins.ultima")]
        );

    public void Register(Container container)
        => container.AddMoongateService<IAdminApiService, AdminGrpcHostService>(
            () =>
            {
                var config = container.Resolve<AdminApiConfig>();
                var mode = container.Resolve<ServerMode>();

                return new(
                    config,
                    container.Resolve<DirectoriesConfig>(),
                    mode,
                    services =>
                        AdminGrpcApplication.AddServices(
                            services,
                            config,
                            container.Resolve<IAdminSessionStore>(),
                            container.Resolve<IAdminLoginThrottle>(),
                            container.Resolve<IAdminServerInfoProvider>(),
                            (mode & ServerMode.Login) != 0 ? container.Resolve<IAccountService>() : null,
                            (mode & ServerMode.Login) != 0 ? container.Resolve<IAccountAdminAccessService>() : null
                        )
                );
            },
            AdminGrpcHostService.StartupPriority
        );
}
