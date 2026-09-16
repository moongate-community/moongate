using System.Runtime.ExceptionServices;
using DryIoc;
using Serilog;

using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Bootstrap;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Bootstrap;

public class MoongateServerBootstrap : IMoongateServerBootstrap
{
    private readonly Container _container;
    private readonly ILogger _logger = Log.ForContext<MoongateServerBootstrap>();
    private readonly CancellationToken _cancellationToken;
    private readonly Lazy<IMoongateEventBus> _eventBus;
    private readonly BootstrapLifecycleTasks _lifecycle = new();
    private readonly StartupServiceLifecycle _services;
    private Task? _gameLoopCompletion;

    public MoongateServerBootstrap(Container container, CancellationToken cancellationToken)
    {
        _container = container;
        _cancellationToken = cancellationToken;
        _container.RegisterMoongateEventBus();
        _eventBus = new Lazy<IMoongateEventBus>(() => _container.Resolve<IMoongateEventBus>());
        _services = new StartupServiceLifecycle(container);
    }

    /// <summary>Configures services immediately and returns this bootstrap for fluent composition.</summary>
    /// <remarks>
    /// Call before starting or stopping the bootstrap. The callback must return the supplied container
    /// and must not reenter registration or lifecycle methods. Exceptions propagate to the caller;
    /// registrations already applied by the callback are not rolled back.
    /// </remarks>
    public MoongateServerBootstrap RegisterServices(Func<Container, Container> registerServices)
    {
        ArgumentNullException.ThrowIfNull(registerServices);
        _lifecycle.Configure(() =>
        {
            if (!ReferenceEquals(registerServices(_container), _container))
            {
                throw new InvalidOperationException("Service registration must return the supplied container.");
            }
        });

        return this;
    }

    public Task StartAsync()
    {
        return _lifecycle.StartAsync(StartCoreAsync);
    }

    public Task StopAsync()
    {
        return _lifecycle.StopAsync(StopAfterStartupAsync);
    }

    public async Task RunAsync()
    {
        var shutdownRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = _cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(), shutdownRequested);

        if (_gameLoopCompletion is null)
        {
            await shutdownRequested.Task.ConfigureAwait(false);
        }
        else
        {
            await Task.WhenAny(_gameLoopCompletion, shutdownRequested.Task).ConfigureAwait(false);

            if (_gameLoopCompletion.IsCompleted)
            {
                await _gameLoopCompletion.ConfigureAwait(false);
                return;
            }
        }

        _logger.Debug("Moongate Server is shutting down due to cancellation request.");
    }

    private async Task StartCoreAsync()
    {
        try
        {
            if (_container.IsRegistered<IPluginLoaderService>())
            {
                _container.Resolve<IPluginLoaderService>().LoadPlugins();
            }

            await _services.StartAsync(service =>
            {
                if (service is IGameLoopService gameLoop)
                {
                    // Capture after priority-ordered resolution, even if a later startup step fails.
                    _gameLoopCompletion = gameLoop.Completion;
                }
            }).ConfigureAwait(false);

            if (_gameLoopCompletion is { IsCompleted: true })
            {
                await _gameLoopCompletion.ConfigureAwait(false);
            }

            await _eventBus.Value.PublishAsync(new MoongateStartedEvent(), _cancellationToken).ConfigureAwait(false);
            _logger.Information("Moongate Server started.");
        }
        catch (Exception exception)
        {
            var cleanupFailures = await _lifecycle.ShutdownAsync(ShutdownCoreAsync).ConfigureAwait(false);
            cleanupFailures.RemoveAll(failure => ReferenceEquals(failure, exception));

            if (cleanupFailures.Count == 0)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            cleanupFailures.Insert(0, exception);

            throw new AggregateException(cleanupFailures);
        }
    }

    private async Task StopAfterStartupAsync(Task? startupTask)
    {
        var startupFailed = false;

        if (startupTask is not null)
        {
            try
            {
                await startupTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                startupFailed = true;
            }
        }

        var failures = await _lifecycle.ShutdownAsync(ShutdownCoreAsync).ConfigureAwait(false);

        if (!startupFailed)
        {
            ThrowFailures(failures);
        }
    }

    private async Task<List<Exception>> ShutdownCoreAsync()
    {
        List<Exception> failures = [];

        await CaptureFailureAsync(
            () => _eventBus.Value.PublishAsync(new MoongateStoppingEvent(), CancellationToken.None), failures
        ).ConfigureAwait(false);

        failures.AddRange(await _services.StopAsync().ConfigureAwait(false));

        if (_gameLoopCompletion is not null)
        {
            await CaptureFailureAsync(() => _gameLoopCompletion, failures).ConfigureAwait(false);
        }

        await CaptureFailureAsync(
            () => _eventBus.Value.PublishAsync(new MoongateStoppedEvent(), CancellationToken.None), failures
        ).ConfigureAwait(false);

        CaptureFailure(_container.Dispose, failures);
        _logger.Information("Moongate Server stopped.");
        await CaptureFailureAsync(
            async () => await Log.CloseAndFlushAsync().ConfigureAwait(false), failures
        ).ConfigureAwait(false);

        return failures;
    }

    private static void CaptureFailure(Action operation, List<Exception> failures)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static async Task CaptureFailureAsync(Func<Task> operation, List<Exception> failures)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void ThrowFailures(List<Exception> failures)
    {
        if (failures.Count == 0)
            return;

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException(failures);
    }
}
