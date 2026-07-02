using ConsoleAppFramework;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Services.Core.Services.Bootstrap;

await ConsoleApp.RunAsync(
    args,
    async (string rootDirectory = null, CancellationToken ct = default) =>
    {
        rootDirectory ??= Path.Join(Directory.GetCurrentDirectory(), "moongate");

        var stdBootstrap = new SquidStdBootstrap(
            new SquidStdOptions()
            {
                ConfigName = "moongate",
                RootDirectory = rootDirectory
            }
        );

        await stdBootstrap.RunAsync(ct);
    }
);
