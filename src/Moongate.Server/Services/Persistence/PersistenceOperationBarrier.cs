using System.Runtime.ExceptionServices;
using Moongate.Server.Core.Interfaces.Persistence;

namespace Moongate.Server.Services.Persistence;

/// <summary>Serializes critical owner operations and saves, retaining unsafe owner-application failures.</summary>
public sealed class PersistenceOperationBarrier : IPersistenceOperationBarrier
{
    private readonly Lock _gate = new();
    private readonly AsyncLocal<bool> _inside = new();
    private Task _tail = Task.CompletedTask;
    private ExceptionDispatchInfo? _failure;
    private bool _closed;

    /// <inheritdoc />
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        return AdmitAsync(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return operation(cancellationToken);
            },
            critical: true,
            allowClosed: false
        );
    }

    internal Task RunSaveAsync(Func<Task> save, bool finalSave = false)
    {
        return AdmitAsync(save, critical: false, allowClosed: finalSave);
    }

    internal async Task CloseAsync()
    {
        EnsureOutsideOperation();
        Task drain;
        lock (_gate)
        {
            _closed = true;
            drain = _tail;
        }

        await drain.ConfigureAwait(false);
        lock (_gate)
        {
            _failure?.Throw();
        }
    }

    private Task AdmitAsync(Func<Task> operation, bool critical, bool allowClosed)
    {
        EnsureOutsideOperation();
        Task previous;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _failure?.Throw();
            if (_closed && !allowClosed)
            {
                throw new InvalidOperationException("Persistence owner operations are closed for shutdown.");
            }

            previous = _tail;
            _tail = finished.Task;
        }

        return RunAsync();

        async Task RunAsync()
        {
            try
            {
                await previous.ConfigureAwait(false);
                lock (_gate)
                {
                    _failure?.Throw();
                }

                _inside.Value = true;
                await operation().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (critical)
                {
                    lock (_gate)
                    {
                        _failure ??= ExceptionDispatchInfo.Capture(exception);
                    }
                }

                throw;
            }
            finally
            {
                _inside.Value = false;
                finished.TrySetResult();
            }
        }
    }

    internal void Capture(Action capture)
    {
        var previous = _inside.Value;
        _inside.Value = true;
        try
        {
            capture();
        }
        finally
        {
            _inside.Value = previous;
        }
    }

    internal void EnsureOutsideOperation()
    {
        if (_inside.Value)
        {
            throw new InvalidOperationException("Persistence owner coordination cannot be reentered from its callback.");
        }
    }
}
