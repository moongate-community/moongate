using System.Diagnostics;
using System.Security.Cryptography;
using DryIoc;
using Moongate.Abstractions.Data.Internal;
using Moongate.Abstractions.Extensions;
using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Abstractions.Types;
using Moongate.Core.Data.Directories;
using Moongate.Core.Extensions.Directories;
using Moongate.Core.Extensions.Logger;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Generated;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Connections;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Version;
using Moongate.Server.Http;
using Moongate.Server.Http.Interfaces;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.Lifecycle;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Json;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.Console.Internal.Logging;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Files;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Version;
using Serilog;
using Serilog.Filters;

namespace Moongate.Server.Bootstrap;

public sealed class MoongateBootstrap : IDisposable
{
    private readonly Container _container = new(Rules.Default.WithUseInterpretation());

    private ILogger _logger;

    private DirectoriesConfig _directoriesConfig;
    private readonly IConsoleUiService _consoleUiService = new ConsoleUiService();
    private readonly MoongateConfig _moongateConfig;

    public MoongateBootstrap(MoongateConfig config)
    {
        _moongateConfig = config;

        CheckDirectoryConfig();

        CreateLogger();
        CheckConfig();
        CheckUODirectory();
        EnsureDataAssets();

        Console.WriteLine("Root Directory: " + _directoriesConfig.Root);

        RegisterHttpServer();
        RegisterScriptUserData();
        RegisterScriptModules();
        RegisterServices();
        RegisterFileLoaders();

        RegisterPacketHandlers();
        RegisterGameEventListeners();

        RegisterDefaultCommands();
    }

    private void RegisterDefaultCommands()
    {
        var commandService = _container.Resolve<ICommandSystemService>();

        commandService.RegisterCommand(
            "send_target",
            async context =>
            {
                var eventBus = _container.Resolve<IGameEventBusService>();

                await eventBus.PublishAsync(
                    new TargetRequestCursorEvent(
                        context.SessionId,
                        TargetCursorSelectionType.SelectLocation,
                        TargetCursorType.Helpful,
                        callback =>
                        {
                            context.Print(
                                "Target cursor callback invoked with selection: {0} ",
                                callback.Packet.Location
                            );
                        }
                    )
                );
            },
            "Sends a target cursor to the specified player. Usage: send_target ",
            CommandSourceType.InGame,
            AccountType.Regular
        );

        commandService.RegisterCommand(
            "orion",
            async context =>
            {
                var eventBus = _container.Resolve<IGameEventBusService>();
                var mobileService = _container.Resolve<IMobileService>();
                var spatialWorldService = _container.Resolve<ISpatialWorldService>();

                await eventBus.PublishAsync(
                    new TargetRequestCursorEvent(
                        context.SessionId,
                        TargetCursorSelectionType.SelectLocation,
                        TargetCursorType.Helpful,
                        callback =>
                        {
                            var mobile = mobileService.SpawnFromTemplateAsync("orione", callback.Packet.Location, 1)
                                                      .GetAwaiter()
                                                      .GetResult();

                            spatialWorldService.AddOrUpdateMobile(mobile);

                            context.Print("Orion the cat: {0} ", callback.Packet.Location);
                        }
                    )
                );
            },
            "Create a cat, beautiful cat",
            CommandSourceType.InGame,
            AccountType.Regular
        );
    }

    public void Dispose()
    {
        _container.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        var serviceRegistrations = _container.Resolve<List<ServiceRegistrationObject>>()
                                             .OrderBy(s => s.Priority)
                                             .ToList();

        var runningServices = new List<IMoongateService>(serviceRegistrations.Count);

        foreach (var serviceRegistration in serviceRegistrations)
        {
            if (_container.Resolve(serviceRegistration.ServiceType) is not IMoongateService instance)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve service of type {serviceRegistration.ServiceType.FullName}"
                );
            }

            _logger.Verbose("Starting {ServiceTypeFullName}", serviceRegistration.ImplementationType.Name);

            try
            {
                await instance.StartAsync();
            }
            catch (Exception ex) when (serviceRegistration.ServiceType == typeof(IFileLoaderService))
            {
                _logger.Error(ex, "Startup aborted: file loader execution failed.");

                if (ex is InvalidOperationException &&
                    ex.Message.StartsWith("Template validation failed", StringComparison.Ordinal))
                {
                    _logger.Error("Template validation failed, server startup aborted.");
                }

                throw;
            }
            runningServices.Add(instance);
        }

        await CheckDefaultAdminAccount();

        _logger.Information("Server started in {StartupTime} ms", Stopwatch.GetElapsedTime(startTime).TotalMilliseconds);
        _logger.Information("Moongate server is running. Press Ctrl+C to stop.");

        var serverLifetimeService = _container.Resolve<IServerLifetimeService>();
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serverLifetimeService.ShutdownToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, linkedCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Shutdown requested.");
        }

        await StopAsync(runningServices);
    }

    private void CheckConfig()
    {
        if (!File.Exists(Path.Combine(_directoriesConfig.Root, "moongate.json")))
        {
            _logger.Warning(
                "No moongate.json configuration file found in root directory. Using default configuration values."
            );

            JsonUtils.SerializeToFile(
                _moongateConfig,
                Path.Combine(_directoriesConfig.Root, "moongate.json"),
                MoongateServerJsonContext.Default
            );
        }

        else
        {
            var fileConfig = JsonUtils.DeserializeFromFile<MoongateConfig>(
                Path.Combine(_directoriesConfig.Root, "moongate.json"),
                MoongateServerJsonContext.Default
            );

            _logger.Information("Loaded configuration from moongate.json in root directory.");

            // Override properties with values from the file if they are not null or default
            if (!string.IsNullOrWhiteSpace(fileConfig.RootDirectory))
            {
                _moongateConfig.RootDirectory = fileConfig.RootDirectory;
            }

            if (!string.IsNullOrWhiteSpace(fileConfig.UODirectory))
            {
                _moongateConfig.UODirectory = fileConfig.UODirectory;
            }

            if (fileConfig.LogLevel != LogLevelType.Information)
            {
                _moongateConfig.LogLevel = fileConfig.LogLevel;
            }

            _moongateConfig.LogPacketData = fileConfig.LogPacketData;

            if (fileConfig.Persistence is not null)
            {
                _moongateConfig.Persistence = fileConfig.Persistence;
            }
        }
    }

    private async Task CheckDefaultAdminAccount()
    {
        var persistenceService = _container.Resolve<IPersistenceService>();
        var accountService = _container.Resolve<IAccountService>();

        if (await persistenceService.UnitOfWork.Accounts.CountAsync() == 0)
        {
            var defaultAdminUsername = Environment.GetEnvironmentVariable("MOONGATE_ADMIN_USERNAME") ?? "admin";
            var defaultAdminPassword = Environment.GetEnvironmentVariable("MOONGATE_ADMIN_PASSWORD") ?? "password";

            await accountService.CreateAccountAsync(
                defaultAdminUsername,
                defaultAdminPassword,
                $"{defaultAdminUsername}@localhost",
                AccountType.Administrator
            );

            _logger.Warning(
                "No accounts found. Created default administrator account with username '{Username}' and password '{Password}'.",
                defaultAdminUsername,
                defaultAdminPassword
            );
        }

        await persistenceService.SaveAsync();
    }

    private void CheckDirectoryConfig()
    {
        if (string.IsNullOrWhiteSpace(_moongateConfig.RootDirectory))
        {
            _moongateConfig.RootDirectory = Environment.GetEnvironmentVariable("MOONGATE_ROOT_DIRECTORY") ??
                                            Path.Combine(AppContext.BaseDirectory, "moongate");
        }

        _moongateConfig.RootDirectory = _moongateConfig.RootDirectory.ResolvePathAndEnvs();

        _directoriesConfig = new(_moongateConfig.RootDirectory, Enum.GetNames<DirectoryType>());
    }

    private void CheckUODirectory()
    {
        if (string.IsNullOrWhiteSpace(_moongateConfig.UODirectory))
        {
            _moongateConfig.UODirectory = Environment.GetEnvironmentVariable("MOONGATE_UO_DIRECTORY");
        }

        if (string.IsNullOrWhiteSpace(_moongateConfig.UODirectory))
        {
            _logger.Error("UO Directory not configured. Set --uoDirectory or MOONGATE_UO_DIRECTORY.");

            throw new InvalidOperationException("UO Directory not configured.");
        }

        UoFiles.RootDir = _moongateConfig.UODirectory.ResolvePathAndEnvs();
        UoFiles.ReLoadDirectory();
        _logger.Information("UO Directory configured in {UODirectory}", UoFiles.RootDir);
    }

    private void CreateLogger()
    {
        var appLogPath = Path.Combine(_directoriesConfig[DirectoryType.Logs], "moongate-.log");
        var packetLogPath = Path.Combine(_directoriesConfig[DirectoryType.Logs], "packets-.log");
        var configuration = new LoggerConfiguration()
                            .MinimumLevel
                            .Is(_moongateConfig.LogLevel.ToSerilogLogLevel())
                            .Enrich
                            .WithDemystifiedStackTraces()
                            .WriteTo
                            .File(
                                appLogPath,
                                rollingInterval: RollingInterval.Day
                            );

        if (_moongateConfig.Metrics.LogToConsole)
        {
            configuration = configuration.WriteTo.Sink(new ConsoleUiSerilogSink(_consoleUiService));
        }
        else
        {
            configuration = configuration.WriteTo.Logger(
                loggerConfiguration =>
                    loggerConfiguration
                        .Filter
                        .ByExcluding(Matching.WithProperty("MetricsData"))
                        .WriteTo
                        .Sink(new ConsoleUiSerilogSink(_consoleUiService))
            );
        }

        if (_moongateConfig.LogPacketData)
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
        _logger = Log.ForContext<MoongateBootstrap>();
    }

    private void EnsureDataAssets()
    {
        var sourceDataDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "data");
        var destinationDataDirectory = _directoriesConfig[DirectoryType.Data];
        var sourceTemplatesDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "templates");
        var destinationTemplatesDirectory = _directoriesConfig[DirectoryType.Templates];

        DataAssetsBootstrapper.EnsureDataAssets(sourceDataDirectory, destinationDataDirectory, _logger);
        DataAssetsBootstrapper.EnsureAssets(
            sourceTemplatesDirectory,
            destinationTemplatesDirectory,
            _logger,
            "Template assets"
        );
    }

    private void RegisterFileLoaders()
    {
        var fileLoaderService = _container.Resolve<IFileLoaderService>();
        BootstrapFileLoaderRegistration.Register(fileLoaderService);
    }

    private void RegisterHttpServer()
    {
        if (!_moongateConfig.Http.IsEnabled)
        {
            _logger.Information("HTTP Server disabled.");

            return;
        }

        _container.RegisterMoongateService<IMoongateHttpService, MoongateHttpService>(ServicePriority.HttpServer);
        _logger.Information("HTTP Server enabled.");
        _container.RegisterInstance(CreateHttpServiceOptions());
    }

    private MoongateHttpServiceOptions CreateHttpServiceOptions()
    {
        var jwtSigningKey = ResolveHttpJwtSigningKey();

        return new()
        {
            DirectoriesConfig = _directoriesConfig,
            IsOpenApiEnabled = _moongateConfig.Http.IsOpenApiEnabled,
            Port = _moongateConfig.Http.Port,
            MinimumLogLevel = _moongateConfig.LogLevel.ToSerilogLogLevel(),
            Jwt = new()
            {
                IsEnabled = _moongateConfig.Http.Jwt.IsEnabled,
                SigningKey = jwtSigningKey,
                Issuer = _moongateConfig.Http.Jwt.Issuer,
                Audience = _moongateConfig.Http.Jwt.Audience,
                ExpirationMinutes = _moongateConfig.Http.Jwt.ExpirationMinutes
            }
        };
    }

    private void RegisterPacketHandlers()
    {
        BootstrapPacketHandlerRegistration.Register(_container);
    }

    private void RegisterGameEventListeners()
    {
        BootstrapGameEventListenerRegistration.Subscribe(_container);
    }

    private void RegisterScriptModules()
    {
        _container.RegisterInstance(
            new LuaEngineConfig(
                Path.Combine(_directoriesConfig.Root, ".luarc"),
                _directoriesConfig[DirectoryType.Scripts],
                VersionUtils.Version
            )
        );
        ScriptModuleRegistry.Register(_container);
        Generated.ScriptModuleRegistry.Register(_container);

        if (!_container.IsRegistered<List<ScriptModuleData>>())
        {
            _container.RegisterInstance(new List<ScriptModuleData>());
        }
    }

    private void RegisterScriptUserData()
    {
        _container.RegisterLuaUserData<PlayerConnectedEvent>();
        _container.RegisterLuaUserData<PlayerDisconnectedEvent>();
        _container.RegisterLuaUserData<ClientVersion>();
    }

    private void RegisterServices()
    {
        BootstrapServiceRegistration.Register(_container, _moongateConfig, _directoriesConfig, _consoleUiService);
    }

    private string ResolveHttpJwtSigningKey()
    {
        var configuredKey = _moongateConfig.Http.Jwt.SigningKey;

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            return configuredKey;
        }

        var envKey = Environment.GetEnvironmentVariable("MOONGATE_HTTP_JWT_SIGNING_KEY");

        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey;
        }

        if (!_moongateConfig.Http.Jwt.IsEnabled)
        {
            return string.Empty;
        }

        Span<byte> buffer = stackalloc byte[64];
        RandomNumberGenerator.Fill(buffer);
        var generated = Convert.ToHexString(buffer);

        _logger.Warning(
            "HTTP JWT is enabled but no signing key was configured. Generated ephemeral key for this process. " +
            "Set MOONGATE_HTTP_JWT_SIGNING_KEY to keep tokens valid across restarts."
        );

        return generated;
    }

    private async Task StopAsync(List<IMoongateService> runningServices)
    {
        for (var i = runningServices.Count - 1; i >= 0; i--)
        {
            var service = runningServices[i];

            _logger.Information("Stopping {ServiceTypeFullName}", service.GetType().Name);
            await service.StopAsync();
        }
    }
}
