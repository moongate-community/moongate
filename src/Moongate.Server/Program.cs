using ConsoleAppFramework;
using SquidStd.Core.Extensions.Env;
using SquidStd.Services.Core.Services.Bootstrap;

await ConsoleApp.RunAsync(
    args,
    async (string rootDirectory = null, CancellationToken ct = default) =>
    {
        rootDirectory ??= "$HOME/.moongate".ReplaceEnv();

        var stdBootstrap = new SquidStdBootstrap(
            new()
            {
                AppName = "Moongate",
                AppVersion = "0.0.1",
                ConfigName = "moongate",
                RootDirectory = rootDirectory
            }
        );

        await stdBootstrap.RunAsync(ct);
    }
);
