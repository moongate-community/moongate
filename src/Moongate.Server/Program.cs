using ConsoleAppFramework;
using SquidStd.Core.Extensions.Env;
using SquidStd.Core.Utils;
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

        await stdBootstrap.RunAsync(ct);
    }
);
