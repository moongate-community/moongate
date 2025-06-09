using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Data.Configs.Services;
using Moongate.Core.Server.Data.Internal.Services;
using Moongate.Core.Server.Data.Options;
using Moongate.Core.Server.Events.Events.Server;
using Moongate.Core.Server.Extensions;
using Moongate.Core.Server.Extensions.Loggers;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.Core.Server.Services;
using Moongate.Core.Server.Types;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Formatting.Compact;

namespace Moongate.Core.Server.Bootstrap;

public class MoongateBootstrap
{
    private readonly IContainer _container;

    private MoongateServerConfig _moongateServerConfig;

    private DirectoriesConfig _directoriesConfig;

    private readonly CancellationToken _stopCancellationToken;
    public event MoongateBootstrapDelegates.ConfigureServicesDelegate? ConfigureServices;
    public event MoongateBootstrapDelegates.ConfigureNetworkServicesDelegate ConfigureNetworkServices;

    public event MoongateBootstrapDelegates.ShutdownRequestDelegate ShutdownRequest;

    private readonly MoongateArgsOptions _argsOptions;

    public MoongateBootstrap(MoongateArgsOptions argsOptions, CancellationToken stopCancellationToken)
    {
        _stopCancellationToken = stopCancellationToken;
        _argsOptions = argsOptions;

        _container = new Container(rules =>
            rules.WithUseInterpretation()
        );
    }

    private void LoadConfig()
    {
        _moongateServerConfig = CheckAndLoadConfig(_argsOptions.ConfigFile);
    }

    public void Initialize()
    {
        ConfigureServices?.Invoke(_container);
        ConfigureDirectories();
        LoadConfig();
        ConfigureLogging();

        if (!CheckUltimaOnlineDirectory())
        {
            throw new DirectoryNotFoundException(
                "Ultima Online directory not found or not set in the configuration."
            );
        }

        ConfigureDefaultServices();
    }

    private void ConfigureDirectories()
    {
        if (string.IsNullOrEmpty(_argsOptions.RootDirectory))
        {
            _argsOptions.RootDirectory = Environment.GetEnvironmentVariable("MOONGATE_ROOT_DIRECTORY") ??
                                         Path.GetFullPath(_argsOptions.RootDirectory);
        }

        _directoriesConfig = new DirectoriesConfig(_argsOptions.RootDirectory, Enum.GetNames<DirectoryType>());
    }

    private void ConfigureLogging()
    {
        var logConfig = new LoggerConfiguration();

        logConfig.WriteTo.Console();

        logConfig.WriteTo.File(
            new CompactJsonFormatter(),
            Path.Combine(_directoriesConfig[DirectoryType.Logs], "moongate_server_.json"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7
        );

        logConfig.MinimumLevel.Is(_argsOptions.LogLevel.ToSerilogLogLevel());

        if (_moongateServerConfig.Network.LogPackets)
        {
            logConfig.WriteTo.Logger(sub => sub
                .Filter.ByIncludingOnly(Matching.FromSource("NetworkPacket"))
                .WriteTo.File(
                    path: Path.Combine(_directoriesConfig[DirectoryType.Logs], "network_packets_.log"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Information
                )
            );
        }

        Log.Logger = logConfig.CreateLogger();
    }

    public bool CheckUltimaOnlineDirectory()
    {
        if (string.IsNullOrEmpty(_moongateServerConfig.UltimaOnlineDirectory))
        {
            Log.Logger.Error("Ultima Online directory is not set in the configuration.");
            return false;
        }

        if (!Directory.Exists(_moongateServerConfig.UltimaOnlineDirectory))
        {
            Log.Logger.Error(
                "Ultima Online directory not found: {UltimaOnlineDirectory}",
                _moongateServerConfig.UltimaOnlineDirectory
            );
            return false;
        }

        return true;
    }

    private void ConfigureDefaultServices()
    {
        // Register configs

        _container.RegisterInstance(_argsOptions);
        _container.RegisterInstance(
            new DiagnosticServiceConfig() { MetricsIntervalInSeconds = 60, PidFileName = "moongate.pid" }
        );
        _container.RegisterInstance(new EventLoopConfig());

        _container
            .AddService(typeof(IEventBusService), typeof(EventBusService))
            .AddService(typeof(IEventDispatcherService), typeof(EventDispatcherService))
            .AddService(typeof(IEventLoopService), typeof(EventLoopService))
            .AddService(typeof(ISchedulerSystemService), typeof(SchedulerSystemService))
            .AddService(typeof(ITimerService), typeof(TimerService))
            ;
    }

    public async Task StartAsync()
    {
        ConfigureNetworkServices?.Invoke(_container.Resolve<INetworkService>());

        while (!_stopCancellationToken.IsCancellationRequested)
        {
            try
            {
                await StartOrStopServices(true);
                await Task.Delay(1000, _stopCancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log.Logger.Information("Shutdown requested, stopping Moongate");
                ShutdownRequest?.Invoke();
                await StopAsync();
            }
        }
    }

    private async Task StartOrStopServices(bool isStart)
    {
        if (isStart)
        {
            await MoongateContext.EventBusService.PublishAsync(new ServerStartingEvent(), _stopCancellationToken);
        }
        else
        {
            await MoongateContext.EventBusService.PublishAsync(new ServerStoppingEvent(), _stopCancellationToken);
        }

        var servicesToLoad = _container.Resolve<List<ServiceDefinitionObject>>();

        foreach (var serviceDefinition in servicesToLoad.OrderBy(s => s.Priority))
        {
            var service = _container.Resolve(serviceDefinition.ServiceType);
            if (service is IMoongateAutostartService startableService)
            {
                if (isStart)
                {
                    Log.Logger.Information(
                        "Starting service: {ServiceName} priority: {Priority}",
                        service.GetType().Name,
                        serviceDefinition.Priority
                    );
                    await startableService.StartAsync(_stopCancellationToken);
                }
                else
                {
                    Log.Logger.Information(
                        "Stopping service: {ServiceName} priority: {Priority}",
                        service.GetType().Name,
                        serviceDefinition.Priority
                    );
                    await startableService.StopAsync(_stopCancellationToken);
                }
            }
        }

        if (isStart)
        {
            await MoongateContext.EventBusService.PublishAsync(new ServerStartedEvent(), _stopCancellationToken);
        }
        else
        {
            await MoongateContext.EventBusService.PublishAsync(new ServerStoppedEvent(), _stopCancellationToken);
        }
    }

    public async Task StopAsync()
    {
        await StartOrStopServices(false);
    }

    private MoongateServerConfig CheckAndLoadConfig(string configName)
    {
        Console.WriteLine($"Loading config: {configName}");
        var config = new MoongateServerConfig();

        var configPath = Path.Combine(_directoriesConfig.Root, configName);

        if (!File.Exists(configPath))
        {
            JsonUtils.SerializeToFile(config, configPath);
        }

        config = JsonUtils.DeserializeFromFile<MoongateServerConfig>(
            configPath
        );

        JsonUtils.SerializeToFile(config, configPath);

        config.UltimaOnlineDirectory ??= _argsOptions.UltimaOnlineDirectory;

        return config;
    }
}
