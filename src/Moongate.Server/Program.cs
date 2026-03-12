using System.Diagnostics;
using ConsoleAppFramework;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Core.Utils;
using Moongate.Scripting.Context;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Json;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Json.Converters;

await ConsoleApp.RunAsync(
    args,
    async (
        bool showHeader = true,
        string rootDirectory = null,
        string uoDirectory = null,
        LogLevelType loglevel = LogLevelType.Debug,
        bool waitForDebugger = false,
        CancellationToken cancellationToken = default
    ) =>
    {
        if (waitForDebugger && !Debugger.IsAttached)
        {
            Console.WriteLine($"Waiting for debugger to attach... (PID: {Environment.ProcessId})");

            while (!Debugger.IsAttached)
            {
                await Task.Delay(250, cancellationToken);
            }

            Console.WriteLine("Debugger attached!");
        }

        var resolvedRootDirectory = RootDirectoryResolver.Resolve(rootDirectory);
        using var pidFileGuard = PidFileGuard.Acquire(resolvedRootDirectory);

        if (showHeader)
        {
            ShowHeader();
        }

        JsonUtils.AddJsonConverter(new ClientVersionConverter());
        JsonUtils.AddJsonConverter(new MapConverter());
        JsonUtils.AddJsonConverter(new Point2DConverter());
        JsonUtils.AddJsonConverter(new Point3DConverter());
        JsonUtils.AddJsonConverter(new ProfessionInfoConverter());
        JsonUtils.AddJsonConverter(new RaceConverter());
        JsonUtils.AddJsonConverter(new SerialConverter());

        JsonUtils.RegisterJsonContext(MoongateUOJsonSerializationContext.Default);
        JsonUtils.RegisterJsonContext(MoongateUOTemplateJsonContext.Default);
        JsonUtils.RegisterJsonContext(MoongateServerJsonContext.Default);
        JsonUtils.RegisterJsonContext(MoongateLuaScriptJsonContext.Default);

        var bootstrap = new MoongateBootstrap(
            new()
            {
                RootDirectory = resolvedRootDirectory,
                LogLevel = loglevel,
                LogPacketData = true,
                UODirectory = uoDirectory
            }
        );

        await bootstrap.RunAsync(cancellationToken);

        Console.WriteLine("\nBye bye!");
    }
);

static void ShowHeader()
{
    var header = ResourceUtils.GetEmbeddedResourceString(typeof(Program).Assembly, "Resources/header.txt");

    Console.WriteLine();
    Console.WriteLine(header);
    Console.WriteLine();
    Console.WriteLine("Platform: " + PlatformUtils.GetCurrentPlatform());
    Console.WriteLine("Is running from Docker: " + PlatformUtils.IsRunningFromDocker());
}
