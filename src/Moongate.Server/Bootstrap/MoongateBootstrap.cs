using System.Diagnostics;
using DryIoc;
using Moongate.Abstractions.Data.Internal;
using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Core.Data.Directories;
using Moongate.Server.Bootstrap.Phases;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Bootstrap;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.Lifecycle;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Services.Console;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Bootstrap;

public sealed class MoongateBootstrap : IDisposable
{
    private readonly Container _container = new(Rules.Default.WithUseInterpretation());

    private readonly ILogger _logger;

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IConsoleUiService _consoleUiService = new ConsoleUiService();
    private readonly MoongateConfig _moongateConfig;

    public MoongateBootstrap(MoongateConfig config)
    {
        _moongateConfig = config;

        var context = new BootstrapContext
        {
            Container = _container,
            Config = _moongateConfig,
            ConsoleUiService = _consoleUiService
        };

        IBootstrapPhase[] phases =
        [
            new InfrastructurePhase(),
            new ServiceRegistrationPhase(),
            new WiringPhase()
        ];

        foreach (var phase in phases.OrderBy(p => p.Order))
        {
            phase.Configure(context);
        }

        _directoriesConfig = context.DirectoriesConfig;
        _logger = context.Logger;

        Console.WriteLine("Root Directory: " + _directoriesConfig.Root);
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
