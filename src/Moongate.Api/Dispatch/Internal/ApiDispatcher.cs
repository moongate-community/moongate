using System.Threading.Channels;
using MessagePack;
using Moongate.Api.Connections.Internal;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Errors;
using Moongate.Api.Data.Internal.Protocol;
using Moongate.Api.Data.Internal.Requests;
using Moongate.Api.Data.Requests;
using Moongate.Api.Exceptions;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Registry;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Types.Protocol;
using Serilog;

namespace Moongate.Api.Dispatch.Internal;

internal sealed class ApiDispatcher
{
    private static readonly ILogger Logger = Log.ForContext<ApiDispatcher>();
    private readonly object _gate = new();
    private readonly IApiConnection _connection;
    private readonly ApiRegistry _registry;
    private readonly ApiOptions _options;
    private readonly ApiOutbox _outbox;
    private readonly SemaphoreSlim _executionSlots;
    private readonly TimeProvider _clock;
    private readonly Action<Exception> _abort;
    private readonly ApiFrameCodec _codec;
    private readonly Channel<ApiInboundCall> _queue;
    private readonly HashSet<ApiInboundCall> _calls = [];
    private bool _accepting = true;
    private bool _faulted;
    private uint _lastIncomingId;
    public Task Completion { get; }

    public ApiDispatcher(
        IApiConnection connection,
        ApiRegistry registry,
        ApiOptions options,
        ApiOutbox outbox,
        SemaphoreSlim executionSlots,
        TimeProvider clock,
        Action<Exception> abort
    )
    {
        if (!registry.IsFrozen)
        {
            throw new InvalidOperationException("Freeze the API registry before accepting requests.");
        }
        options.Validate();
        _connection = connection;
        _registry = registry;
        _options = options with { };
        _outbox = outbox;
        _executionSlots = executionSlots;
        _clock = clock;
        _abort = abort;
        _codec = new(options.MaxFrameLength);
        _queue = Channel.CreateBounded<ApiInboundCall>(
            new BoundedChannelOptions(options.IncomingQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                AllowSynchronousContinuations = false
            }
        );
        Completion = ConsumeAsync();
    }

    public void CancelAll()
    {
        ApiInboundCall[] calls;

        lock (_gate)
        {
            _accepting = false;
            _queue.Writer.TryComplete();
            calls = _calls.ToArray();
        }

        foreach (var call in calls)
        {
            if (call.TryFinish()) { SendError(call.Envelope, ApiErrorCode.Unavailable); }
            call.Cancel();
        }
    }

    public void StopAdmission()
    {
        lock (_gate)
        {
            _accepting = false;
            _queue.Writer.TryComplete();
        }
    }

    public bool TryDispatch(ApiEnvelope request)
    {
        var arrived = _clock.GetTimestamp();
        ApiErrorCode? rejection = null;
        ApiInboundCall? call = null;

        lock (_gate)
        {
            if (request.Kind != ApiMessageKind.Request || request.RequestId <= _lastIncomingId)
            {
                throw new ApiProtocolException("Incoming request identifiers must strictly increase.");
            }
            _lastIncomingId = request.RequestId;

            if (!_accepting) { rejection = ApiErrorCode.Unavailable; }
            else if (!_registry.TryGet(request.OperationId, out var operation) || !operation.HasHandler)
            {
                rejection = ApiErrorCode.UnsupportedOperation;
            }
            else if (!_connection.Peer.CanInvoke(request.OperationId)) { rejection = ApiErrorCode.Forbidden; }
            else
            {
                call = new(new(request.Kind, request.RequestId, request.OperationId, request.Payload.ToArray()), operation);

                if (!_queue.Writer.TryWrite(call)) { rejection = ApiErrorCode.Busy; }
                else { _calls.Add(call); }
            }
        }

        if (rejection is { } code)
        {
            if (call is not null)
            {
                call.TryFinish();

                // No timer, token registration or handler exists for a rejected entry.
                call.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            SendError(request, code);

            return false;
        }
        call!.Arm(_clock, _options.HandlerTimeout - _clock.GetElapsedTime(arrived), Expire);

        return true;
    }

    private async Task ConsumeAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        await foreach (var call in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var acquired = false;

            try
            {
                if (call.IsTerminal) { continue; }
                await _executionSlots.WaitAsync(call.Token).ConfigureAwait(false);
                acquired = true;

                if (!call.IsTerminal) { await ExecuteAsync(call).ConfigureAwait(false); }
            }
            catch (OperationCanceledException) when (call.Token.IsCancellationRequested) { }
            catch (Exception exception) { Fail(exception); }
            finally
            {
                if (acquired) { _executionSlots.Release(); }

                lock (_gate) { _calls.Remove(call); }

                try { await call.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception)
                {
                    Logger.Warning(
                        "API cancellation callback failed for peer {PeerId}, operation {OperationId}, request {RequestId}: {ErrorType}",
                        _connection.Peer.PeerId,
                        call.Envelope.OperationId,
                        call.Envelope.RequestId,
                        exception.GetType().Name
                    );
                }
            }
        }
    }

    private async Task ExecuteAsync(ApiInboundCall call)
    {
        object request;

        try { request = call.Operation.DeserializeRequest(call.Envelope.Payload); }
        catch (MessagePackSerializationException)
        {
            if (call.TryFinish()) { SendError(call.Envelope, ApiErrorCode.InvalidRequest); }

            return;
        }
        catch (ApiProtocolException exception)
        {
            Fail(exception);

            return;
        }

        if (call.IsTerminal) { return; }

        try
        {
            var context = new ApiRequestContext(_connection, call.Envelope.RequestId, call.Envelope.OperationId);
            var response = await call.Operation
                                     .InvokeAsync(context, request, _options.MaxFrameLength, call.Token)
                                     .ConfigureAwait(false);

            if (call.IsTerminal) { return; }
            var frame = _codec.Encode(
                new(ApiMessageKind.Response, call.Envelope.RequestId, call.Envelope.OperationId, response)
            );

            if (call.TryFinish()) { Send(frame); }
        }
        catch (Exception exception)
        {
            if (call.TryFinish()) { SendError(call.Envelope, ApiErrorCode.InternalError); }
            Logger.Warning(
                "API handler failed for peer {PeerId}, operation {OperationId}, request {RequestId}: {ErrorType}",
                _connection.Peer.PeerId,
                call.Envelope.OperationId,
                call.Envelope.RequestId,
                exception.GetType().Name
            );
        }
    }

    private void Expire(ApiInboundCall call)
    {
        if (call.TryFinish())
        {
            SendError(call.Envelope, ApiErrorCode.DeadlineExceeded);
            call.Cancel();
        }
    }

    private void Fail(Exception exception)
    {
        ApiInboundCall[] calls;

        lock (_gate)
        {
            if (_faulted) { return; }
            _faulted = true;
            _accepting = false;
            _queue.Writer.TryComplete();
            calls = _calls.ToArray();
        }

        foreach (var call in calls)
        {
            call.TryFinish();
            call.Cancel();
        }
        _abort(exception);
    }

    private void Send(byte[] frame)
    {
        if (!_outbox.TryEnqueue(new(frame, null))) { Fail(new IOException("The API response queue is full or closed.")); }
    }

    private void SendError(ApiEnvelope request, ApiErrorCode code)
    {
        try
        {
            var payload = ApiPayloadSerializer.Serialize(
                new ApiError { Code = code, Message = code.ToString() },
                _options.MaxFrameLength
            );
            Send(_codec.Encode(new(ApiMessageKind.Error, request.RequestId, request.OperationId, payload)));
        }
        catch (Exception exception) { Fail(exception); }
    }
}
