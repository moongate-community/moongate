using Moongate.Server.Core.Interfaces.Events;
using Serilog;

namespace Moongate.Server.Core.Services.Events.Internal;

internal sealed class MoongateEventBus : IMoongateEventBus, IDisposable
{
    private readonly Lock _sync = new();
    private readonly Dictionary<Type, List<MoongateEventRegistration>> _registrations = new();
    private readonly List<MoongateEventRegistration> _catchAllRegistrations = new();
    private readonly ILogger _logger = Log.ForContext<MoongateEventBus>();
    private bool _isDisposed;

    public async Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class, IMoongateEvent
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        MoongateEventRegistration[] registrations;
        MoongateEventRegistration[] catchAllRegistrations;

        lock (_sync)
        {
            ThrowIfDisposed();
            registrations = _registrations.TryGetValue(typeof(TEvent), out var registered)
                                ? registered.ToArray()
                                : [];
            catchAllRegistrations = _catchAllRegistrations.Count == 0 ? [] : _catchAllRegistrations.ToArray();
        }

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InvokeHandlerAsync(registration, message, cancellationToken).ConfigureAwait(false);
        }

        foreach (var registration in catchAllRegistrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InvokeHandlerAsync<IMoongateEvent>(registration, message, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IMoongateEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        var eventType = typeof(TEvent);
        var registration = new MoongateEventRegistration(handler);

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!_registrations.TryGetValue(eventType, out var registrations))
            {
                registrations = [];
                _registrations.Add(eventType, registrations);
            }

            registrations.Add(registration);
        }

        return new MoongateEventSubscription(target => Unsubscribe(eventType, target), registration);
    }

    public IDisposable SubscribeAll(Func<IMoongateEvent, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var registration = new MoongateEventRegistration(handler);

        lock (_sync)
        {
            ThrowIfDisposed();
            _catchAllRegistrations.Add(registration);
        }

        return new MoongateEventSubscription(UnsubscribeCatchAll, registration);
    }

    internal void Unsubscribe(Type eventType, MoongateEventRegistration registration)
    {
        RemoveUnderLock(
                () =>
                {
                    if (!_registrations.TryGetValue(eventType, out var registrations))
                    {
                        return;
                    }

                    registrations.Remove(registration);

                    if (registrations.Count == 0)
                    {
                        _registrations.Remove(eventType);
                    }
                }
            );
    }

    internal void UnsubscribeCatchAll(MoongateEventRegistration registration)
    {
        RemoveUnderLock(() => _catchAllRegistrations.Remove(registration));
    }

    private void RemoveUnderLock(Action remove)
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            remove();
        }
    }

    private async Task InvokeHandlerAsync<TMessage>(
        MoongateEventRegistration registration,
        TMessage message,
        CancellationToken cancellationToken
    )
    {
        var handler = (Func<TMessage, CancellationToken, Task>)registration.Handler;

        try
        {
            await handler(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw;
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Moongate event observer {HandlerType}.{HandlerMethod} failed for {EventType}.",
                handler.Method.DeclaringType?.FullName ?? "<unknown>",
                handler.Method.Name,
                message?.GetType().FullName
            );
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _registrations.Clear();
            _catchAllRegistrations.Clear();
        }
    }
}
