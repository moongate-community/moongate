using ConsoleAppFramework;
using DryIoc;
using Moongate.Server.Data.Config;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces;
using Moongate.Server.Services;
using Moongate.Server.Services.Network;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Extensions.Env;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Extensions;
using SquidStd.Services.Core.Services.Bootstrap;

const int loginHandoffTtlMs = 30_000;

await ConsoleApp.RunAsync(
    args,
    async (string rootDirectory = null, bool showHeader = true, CancellationToken ct = default) =>
    {
        rootDirectory ??= "$HOME/.moongate".ReplaceEnv();

        if (showHeader)
        {
            var headerFile = ResourceUtils.GetEmbeddedResourceString(typeof(Program).Assembly, "Assets/header.txt");

            Console.WriteLine(headerFile);
        }

        var stdBootstrap = new SquidStdBootstrap(
            new()
            {
                AppName = "Moongate",
                AppVersion = "0.0.1",
                ConfigName = "moongate",
                RootDirectory = rootDirectory
            }
        );

        stdBootstrap.UsePlugins(
            builder =>
            {
                builder.FromDirectory("plugins");
            }
        );

        stdBootstrap.ConfigureServices(
            container =>
            {
                container.RegisterConfigSection<MoongateConfig>("moongate");
                container.Register<IAccountService, StubAccountService>(Reuse.Singleton);
                container.RegisterInstance<IPendingLoginStore>(
                    new PendingLoginStore(loginHandoffTtlMs, () => Environment.TickCount64)
                );
                container.Register<ISessionManager, SessionManager>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, LoginSeedHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, AccountLoginHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, SelectServerHandler>(Reuse.Singleton);
                container.RegisterStdService<INetworkService, NetworkService>();

                return container;
            }
        );

        await stdBootstrap.RunAsync(ct);
    }
);
