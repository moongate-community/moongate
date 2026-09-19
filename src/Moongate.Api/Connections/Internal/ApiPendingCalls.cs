using Moongate.Api.Data.Config;
using Moongate.Api.Data.Errors;
using Moongate.Api.Data.Internal.Requests;
using Moongate.Api.Exceptions;
using Moongate.Api.Interfaces.Internal.Registry;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Types.Protocol;

namespace Moongate.Api.Connections.Internal;

internal sealed class ApiPendingCalls
{
    private readonly ApiOutbox _outbox;
    private readonly ApiOptions _options;
    private readonly TimeProvider _clock;
    private readonly ApiFrameCodec _codec;
    private readonly HashSet<ApiPendingCall> _reservations = [];
    private readonly Dictionary<uint, ApiPendingCall> _pending = [];
    private uint _lastAssignedId;
    private Exception? _closed;
    private bool _accepting = true;
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public uint LastAssignedId
    {
        get
        {
            lock (_outbox.SyncRoot) { return _lastAssignedId; }
        }
    }

    public int Count
    {
        get
        {
            lock (_outbox.SyncRoot) { return _reservations.Count; }
        }
    }

    public Task Completion { get; }

    public ApiPendingCalls(ApiOutbox outbox, ApiOptions options, TimeProvider clock, uint initialRequestId = 0)
    {
        options.Validate();
        _outbox = outbox;
        _options = options with { };
        _clock = clock;
        _codec = new(options.MaxFrameLength);
        _lastAssignedId = initialRequestId;
        Completion = WatchOutboxAsync();
    }

    public void FailAll(Exception error)
    {
        ApiPendingCall[] calls;

        lock (_outbox.SyncRoot)
        {
            _closed ??= error;
            _accepting = false;
            calls = _reservations.ToArray();

            foreach (var call in calls) { Remove(call); }
            _drained.TrySetResult();
        }

        foreach (var call in calls)
        {
            call.Release();
            call.Source.TrySetException(error);
        }
    }

    public bool IsPendingResponse(uint requestId, ushort operationId)
    {
        lock (_outbox.SyncRoot)
        {
            if (requestId == 0 || requestId > _lastAssignedId)
            {
                throw new ApiProtocolException("Response references a never-assigned request.");
            }

            if (!_pending.TryGetValue(requestId, out var call)) { return false; }

            if (call.Operation.Id != operationId)
            {
                throw new ApiProtocolException("Response operation does not match the request.");
            }

            return true;
        }
    }

    public Task<object> RequestAsync(
        IApiOperationRegistration operation,
        object request,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        var duration = timeout ?? _options.CallTimeout;
        ApiOptions.ValidateTimeout(duration);
        cancellationToken.ThrowIfCancellationRequested();
        var call = new ApiPendingCall(operation);
        var started = _clock.GetTimestamp();

        lock (_outbox.SyncRoot)
        {
            if (!_accepting || _closed is not null) { throw new IOException("The API connection is closed.", _closed); }

            if (_reservations.Count >= _options.MaxPendingCalls) { throw new ApiBusyException(); }
            _reservations.Add(call);
        }

        try
        {
            var remaining = duration - _clock.GetElapsedTime(started);
            var timer = _clock.CreateTimer(
                _ => Fail(call, new TimeoutException("The API call deadline elapsed.")),
                null,
                remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                Timeout.InfiniteTimeSpan
            );
            var cancellation = cancellationToken.UnsafeRegister(_ => Cancel(call, cancellationToken), null);
            bool active;

            lock (_outbox.SyncRoot)
            {
                active = _reservations.Contains(call);

                if (active)
                {
                    call.Deadline = timer;
                    call.Cancellation = cancellation;
                }
            }

            if (!active)
            {
                timer.Dispose();
                cancellation.Unregister();

                return call.Source.Task;
            }
            var payload = operation.SerializeRequest(request, _options.MaxFrameLength);

            lock (_outbox.SyncRoot)
            {
                if (!_reservations.Contains(call)) { return call.Source.Task; }
                var id = ReserveNextId();
                var bytes = _codec.Encode(new(ApiMessageKind.Request, id, operation.Id, payload));
                call.RequestId = id;
                _pending.Add(id, call);

                if (!_outbox.TryEnqueue(new(bytes, id))) { throw new ApiBusyException(); }
            }
        }
        catch (Exception exception) { Fail(call, exception); }

        return call.Source.Task;
    }

    public uint ReserveNextId()
    {
        lock (_outbox.SyncRoot)
        {
            if (_lastAssignedId == uint.MaxValue)
            {
                throw new InvalidOperationException("Request identifiers are exhausted; drain and reconnect.");
            }

            return ++_lastAssignedId;
        }
    }

    public Task StopAdmission()
    {
        lock (_outbox.SyncRoot)
        {
            _accepting = false;

            if (_reservations.Count == 0) { _drained.TrySetResult(); }

            return _drained.Task;
        }
    }

    public bool TryComplete(uint requestId, ushort operationId, ReadOnlyMemory<byte> response, ApiError? error)
    {
        ApiPendingCall call;

        lock (_outbox.SyncRoot)
        {
            if (requestId == 0 || requestId > _lastAssignedId)
            {
                throw new ApiProtocolException("Response references a never-assigned request.");
            }

            if (!_pending.TryGetValue(requestId, out call!)) { return false; }

            if (call.Operation.Id != operationId)
            {
                throw new ApiProtocolException("Response operation does not match the request.");
            }
            Remove(call);
        }
        call.Release();

        if (error is not null)
        {
            call.Source.TrySetException(new ApiRemoteException(error.Code, error.Message));

            return true;
        }

        try { call.Source.TrySetResult(call.Operation.DeserializeResponse(response)); }
        catch (Exception exception)
        {
            var failure = new ApiProtocolException("Invalid typed API response.", exception);
            call.Source.TrySetException(failure);

            throw failure;
        }

        return true;
    }

    private void Cancel(ApiPendingCall call, CancellationToken token)
    {
        lock (_outbox.SyncRoot)
        {
            if (!Remove(call)) { return; }
        }
        call.Release();
        call.Source.TrySetCanceled(token);
    }

    private void Fail(ApiPendingCall call, Exception error)
    {
        lock (_outbox.SyncRoot)
        {
            if (!Remove(call)) { return; }
        }
        call.Release();
        call.Source.TrySetException(error);
    }

    private bool Remove(ApiPendingCall call)
    {
        if (!_reservations.Remove(call)) { return false; }

        if (!_accepting && _reservations.Count == 0) { _drained.TrySetResult(); }

        if (call.RequestId != 0)
        {
            _pending.Remove(call.RequestId);
            _outbox.TryRemove(call.RequestId);
        }

        return true;
    }

    private async Task WatchOutboxAsync()
    {
        try { await _outbox.Completion.ConfigureAwait(false); }
        catch (Exception exception)
        {
            FailAll(new IOException("The API transport failed.", exception));

            return;
        }
        FailAll(new IOException("The API transport stopped."));
    }
}
