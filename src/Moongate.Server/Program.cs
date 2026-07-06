using ConsoleAppFramework;
using DryIoc;
using Moongate.Server.Data.Config;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces;
using Moongate.Server.Services;
using Moongate.Server.Services.Network;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Core.Extensions.Env;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Extensions;
using SquidStd.Services.Core.Services.Bootstrap;

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
                    new PendingLoginStore(30000, () => Environment.TickCount64)
                );
                container.Register<ISessionManager, SessionManager>(Reuse.Singleton);
                container.Register<INetworkService, NetworkService>(Reuse.Singleton);

                return container;
            }
        );

        stdBootstrap.OnConfigReady(
            _ =>
            {
                var container = stdBootstrap.Container;
                var network = container.Resolve<INetworkService>();
                var accounts = container.Resolve<IAccountService>();
                var pendingLogins = container.Resolve<IPendingLoginStore>();
                var config = container.Resolve<MoongateConfig>();

                network.RegisterHandler(new LoginSeedHandler());
                network.RegisterHandler(new AccountLoginHandler(accounts, config));
                network.RegisterHandler(new SelectServerHandler(pendingLogins, config));

                network.StartAsync().GetAwaiter().GetResult();
            }
        );

        await stdBootstrap.RunAsync(ct);
    }
);
