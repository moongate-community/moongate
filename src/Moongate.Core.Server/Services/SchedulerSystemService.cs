using System.Collections.Concurrent;
using System.Diagnostics;
using Moongate.Core.Server.Data.Scheduler;
using Moongate.Core.Server.Events.Events.Scheduler;
using Moongate.Core.Server.Interfaces.EventBus;
using Moongate.Core.Server.Interfaces.Services;
using Serilog;

namespace Moongate.Core.Server.Services;

public class SchedulerSystemService : ISchedulerSystemService, IEventBusListener<AddSchedulerJobEvent>
{
    private readonly ILogger _logger = Log.ForContext<SchedulerSystemService>();
    private readonly ConcurrentDictionary<string, ScheduledJobData> _jobs;
    private readonly ConcurrentDictionary<string, IDisposable> _pausedJobs;

    public SchedulerSystemService(IEventBusService eventBusService)
    {
        _jobs = new();
        _pausedJobs = new();
        eventBusService.Subscribe(this);
    }

    public void Dispose()
    {
        foreach (var job in _jobs.Values)
        {
            job.Subscription?.Dispose();
        }

        _jobs.Clear();
        _pausedJobs.Clear();
        GC.SuppressFinalize(this);
    }

    public async Task HandleAsync(AddSchedulerJobEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.Information("Registering job '{JobName}'", @event.Name);
        _ = RegisterJob(@event.Name, @event.Action, @event.TotalSpan);
    }

    public Task<bool> IsJobRegistered(string name)
        => Task.FromResult(_jobs.ContainsKey(name));

    public async Task PauseJob(string name)
    {
        if (!await IsJobRegistered(name))
        {
            throw new InvalidOperationException($"Job '{name}' is not registered");
        }

        if (_jobs.TryGetValue(name, out var job))
        {
            job.Subscription?.Dispose();
            _pausedJobs.TryAdd(name, job.Subscription);
        }
    }

    public async Task RegisterJob(string name, Func<Task> task, TimeSpan interval)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Job name cannot be empty", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(task);

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Interval must be positive", nameof(interval));
        }

        if (await IsJobRegistered(name))
        {
            throw new InvalidOperationException($"Job '{name}' is already registered");
        }

        var subscription = System.Reactive
                                 .Linq
                                 .Observable
                                 .Interval(interval)
                                 .Subscribe(
                                     async _ =>
                                     {
                                         try
                                         {
                                             await ExecuteJob(_jobs[name]);
                                         }
                                         catch (Exception ex)
                                         {
                                             // Log the exception or handle it according to your needs
                                             _logger.Error(ex, "Error occurred while executing job '{JobName}'", name);
                                         }
                                     }
                                 );

        var job = new ScheduledJobData
        {
            Name = name,
            Interval = interval,
            Task = task,
            Subscription = subscription
        };

        _jobs.TryAdd(name, job);
    }

    public async Task ResumeJob(string name)
    {
        if (!await IsJobRegistered(name))
        {
            throw new InvalidOperationException($"Job '{name}' is not registered");
        }

        if (_jobs.TryGetValue(name, out var job))
        {
            var subscription = System.Reactive
                                     .Linq
                                     .Observable
                                     .Interval(job.Interval)
                                     .Subscribe(async _ => await ExecuteJob(_jobs[name]));

            job.Subscription = subscription;
            _pausedJobs.TryRemove(name, out _);
        }
    }

    public async Task UnregisterJob(string name)
    {
        if (await IsJobRegistered(name))
        {
            if (_jobs.TryRemove(name, out var job))
            {
                job.Subscription?.Dispose();
            }
        }
    }

    private async Task ExecuteJob(ScheduledJobData jobData)
    {
        var startTime = Stopwatch.GetTimestamp();
        _logger.Verbose("Executing job '{JobName}'", jobData.Name);
        await jobData.Task();
        var elapsed = Stopwatch.GetElapsedTime(startTime);

        _logger.Verbose("Job '{JobName}' executed in {Elapsed} ms", jobData.Name, elapsed);
    }
}
