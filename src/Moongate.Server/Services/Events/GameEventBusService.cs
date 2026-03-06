using System.Collections.Concurrent;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Interfaces.Services.Events;
using Serilog;

namespace Moongate.Server.Services.Events;

public sealed class GameEventBusService : IGameEventBusService
{
    private readonly ConcurrentDictionary<Type, List<object>> _listeners = new();
    private readonly ILogger _logger = Log.ForContext<GameEventBusService>();

    public async ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
        where TEvent : IGameEvent
    {
        _logger.Verbose("Publishing game event type {EventType} dump: {Value}", typeof(TEvent).Name, gameEvent);

        var typedList = _listeners.TryGetValue(typeof(TEvent), out var typed) ? typed : null;
        var globalList = typeof(TEvent) != typeof(IGameEvent) && _listeners.TryGetValue(typeof(IGameEvent), out var global)
                             ? global
                             : null;

        var typedCount = 0;
        var globalCount = 0;

        if (typedList is not null)
        {
            lock (typedList)
            {
                typedCount = typedList.Count;
            }
        }

        if (globalList is not null)
        {
            lock (globalList)
            {
                globalCount = globalList.Count;
            }
        }

        var totalCount = typedCount + globalCount;

        if (totalCount == 0)
        {
            return;
        }

        // Single-listener fast path: no Task[] allocation
        if (totalCount == 1)
        {
            object listener;

            if (typedCount == 1)
            {
                lock (typedList!)
                {
                    listener = typedList[0];
                }
            }
            else
            {
                lock (globalList!)
                {
                    listener = globalList[0];
                }
            }

            await DispatchListenerSafeAsync(listener, gameEvent, cancellationToken);

            return;
        }

        // Multi-listener path: parallel dispatch (preserves concurrency)
        var tasks = new Task[totalCount];
        var taskIndex = 0;

        if (typedCount > 0)
        {
            lock (typedList!)
            {
                for (var i = 0; i < Math.Min(typedCount, typedList.Count); i++)
                {
                    tasks[taskIndex++] = DispatchListenerSafeAsync(typedList[i], gameEvent, cancellationToken);
                }
            }
        }

        if (globalCount > 0)
        {
            lock (globalList!)
            {
                for (var i = 0; i < Math.Min(globalCount, globalList.Count); i++)
                {
                    tasks[taskIndex++] = DispatchListenerSafeAsync(globalList[i], gameEvent, cancellationToken);
                }
            }
        }

        if (taskIndex < tasks.Length)
        {
            await Task.WhenAll(tasks.AsSpan(0, taskIndex).ToArray());
        }
        else
        {
            await Task.WhenAll(tasks);
        }
    }

    public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
    {
        var listeners = _listeners.GetOrAdd(typeof(TEvent), static _ => []);

        lock (listeners)
        {
            if (listeners.Contains(listener))
            {
                return;
            }

            listeners.Add(listener);
        }
    }

    private async Task DispatchListenerSafeAsync<TEvent>(
        object listenerObject,
        TEvent gameEvent,
        CancellationToken cancellationToken
    )
        where TEvent : IGameEvent
    {
        try
        {
            if (listenerObject is IGameEventListener<TEvent> typedListener)
            {
                await typedListener.HandleAsync(gameEvent, cancellationToken);

                return;
            }

            if (listenerObject is IGameEventListener<IGameEvent> globalListener)
            {
                await globalListener.HandleAsync(gameEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Game event listener failed for event type {EventType}", typeof(TEvent).Name);
        }
    }
}
