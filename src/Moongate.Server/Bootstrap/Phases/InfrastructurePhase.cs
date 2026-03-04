using Moongate.Core.Extensions.Directories;
using Moongate.Core.Extensions.Logger;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Bootstrap;
using Moongate.Server.Json;
using Moongate.Server.Services.Console.Internal.Logging;
using Moongate.UO.Data.Files;
using Serilog;
using Serilog.Filters;

namespace Moongate.Server.Bootstrap.Phases;

/// <summary>
/// Bootstrap phase 1: directories, logger, configuration, UO client data, and asset copying.
/// </summary>
internal sealed class InfrastructurePhase : IBootstrapPhase
{
    public int Order => 1;

    public string Name => "Infrastructure";

    public void Configure(BootstrapContext context)
    {
        CheckDirectoryConfig(context);
        CreateLogger(context);
        CheckConfig(context);
        CheckUODirectory(context);
        EnsureDataAssets(context);
    }

    private static void CheckConfig(BootstrapContext context)
    {
        var configPath = Path.Combine(context.DirectoriesConfig.Root, "moongate.json");

        if (!File.Exists(configPath))
        {
            context.Logger.Warning(
                "No moongate.json configuration file found in root directory. Using default configuration values."
            );

            JsonUtils.SerializeToFile(
                context.Config,
                configPath,
                MoongateServerJsonContext.Default
            );
        }
        else
        {
            var fileConfig = JsonUtils.DeserializeFromFile<MoongateConfig>(
                configPath,
                MoongateServerJsonContext.Default
            );

            context.Logger.Information("Loaded configuration from moongate.json in root directory.");

            if (!string.IsNullOrWhiteSpace(fileConfig.RootDirectory))
            {
                context.Config.RootDirectory = fileConfig.RootDirectory;
            }

            if (!string.IsNullOrWhiteSpace(fileConfig.UODirectory))
            {
                context.Config.UODirectory = fileConfig.UODirectory;
            }

            if (fileConfig.LogLevel != LogLevelType.Information)
            {
                context.Config.LogLevel = fileConfig.LogLevel;
            }

            context.Config.LogPacketData = fileConfig.LogPacketData;

            if (fileConfig.Persistence is not null)
            {
                context.Config.Persistence = fileConfig.Persistence;
            }

            if (fileConfig.Http is not null)
            {
                context.Config.Http = fileConfig.Http;
            }

            if (fileConfig.Email is not null)
            {
                context.Config.Email = fileConfig.Email;
            }

            if (fileConfig.Scripting is not null)
            {
                context.Config.Scripting = fileConfig.Scripting;
            }
        }
    }

    private static void CheckDirectoryConfig(BootstrapContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Config.RootDirectory))
        {
            context.Config.RootDirectory = Environment.GetEnvironmentVariable("MOONGATE_ROOT_DIRECTORY") ??
                                           Path.Combine(AppContext.BaseDirectory, "moongate");
        }

        context.Config.RootDirectory = context.Config.RootDirectory.ResolvePathAndEnvs();

        context.DirectoriesConfig = new(context.Config.RootDirectory, Enum.GetNames<DirectoryType>());
    }

    private static void CheckUODirectory(BootstrapContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Config.UODirectory))
        {
            context.Config.UODirectory = Environment.GetEnvironmentVariable("MOONGATE_UO_DIRECTORY");
        }

        if (string.IsNullOrWhiteSpace(context.Config.UODirectory))
        {
            context.Logger.Error("UO Directory not configured. Set --uoDirectory or MOONGATE_UO_DIRECTORY.");

            throw new InvalidOperationException("UO Directory not configured.");
        }

        UoFiles.RootDir = context.Config.UODirectory.ResolvePathAndEnvs();
        UoFiles.ReLoadDirectory();
        context.Logger.Information("UO Directory configured in {UODirectory}", UoFiles.RootDir);
    }

    private static void CreateLogger(BootstrapContext context)
    {
        var appLogPath = Path.Combine(context.DirectoriesConfig[DirectoryType.Logs], "moongate-.log");
        var packetLogPath = Path.Combine(context.DirectoriesConfig[DirectoryType.Logs], "packets-.log");
        var configuration = new LoggerConfiguration()
                            .MinimumLevel
                            .Is(context.Config.LogLevel.ToSerilogLogLevel())
                            .Enrich
                            .WithDemystifiedStackTraces()
                            .WriteTo
                            .File(
                                appLogPath,
                                rollingInterval: RollingInterval.Day
                            );

        if (context.Config.Metrics.LogToConsole)
        {
            configuration = configuration.WriteTo.Sink(new ConsoleUiSerilogSink(context.ConsoleUiService));
        }
        else
        {
            configuration = configuration.WriteTo.Logger(
                loggerConfiguration =>
                    loggerConfiguration
                        .Filter
                        .ByExcluding(Matching.WithProperty("MetricsData"))
                        .WriteTo
                        .Sink(new ConsoleUiSerilogSink(context.ConsoleUiService))
            );
        }

        if (context.Config.LogPacketData)
        {
            configuration = configuration.WriteTo.Logger(
                loggerConfiguration =>
                    loggerConfiguration
                        .Filter
                        .ByIncludingOnly(Matching.WithProperty("PacketData"))
                        .WriteTo
                        .File(
                            packetLogPath,
                            rollingInterval: RollingInterval.Day,
                            outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                        )
            );
        }

        Log.Logger = configuration.CreateLogger();
        context.Logger = Log.ForContext<MoongateBootstrap>();
    }

    private static void EnsureDataAssets(BootstrapContext context)
    {
        var sourceDataDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "data");
        var destinationDataDirectory = context.DirectoriesConfig[DirectoryType.Data];
        var sourceTemplatesDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "templates");
        var destinationTemplatesDirectory = context.DirectoriesConfig[DirectoryType.Templates];

        DataAssetsBootstrapper.EnsureDataAssets(sourceDataDirectory, destinationDataDirectory, context.Logger);
        DataAssetsBootstrapper.EnsureAssets(
            sourceTemplatesDirectory,
            destinationTemplatesDirectory,
            context.Logger,
            "Template assets"
        );
    }
}
