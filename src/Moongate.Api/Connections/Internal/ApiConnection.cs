using MessagePack;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Errors;
using Moongate.Api.Data.Internal.Protocol;
using Moongate.Api.Data.Security;
using Moongate.Api.Dispatch.Internal;
using Moongate.Api.Exceptions;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Interfaces.Contracts;
using Moongate.Api.Registry;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Types.Protocol;
using Moongate.Network.Interfaces.Client;
using Serilog;
namespace Moongate.Api.Connections.Internal;

internal sealed class ApiConnection : IApiConnection
{
    private static readonly ILogger Logger = Log.ForContext<ApiConnection>();
    private readonly object _gate = new();
    private readonly INetworkConnection _transport;
    private readonly ApiRegistry _registry;
    private readonly ApiOptions _options;
    private readonly TimeProvider _clock;
    private readonly ApiFrameCodec _codec;
    private readonly ApiOutbox _outbox;
    private readonly ApiPendingCalls _pending;
    private readonly ApiDispatcher _dispatcher;
    private Task? _drain;
    private int _closing;
    private long _lateResponses;
    public long ConnectionId => _transport.SessionId;
    public ApiPeerIdentity Peer { get; }
    public Task Completion { get; }
    internal bool IsConnected => _transport.IsConnected;
    internal long LateResponseCount => Interlocked.Read(ref _lateResponses);

    public ApiConnection(INetworkConnection transport, ApiPeerIdentity peer, ApiRegistry registry, ApiOptions options, SemaphoreSlim executionSlots, TimeProvider clock)
    {
        _transport = transport;
        Peer = peer;
        _registry = registry;
        _options = options with { };
        _clock = clock;
        _codec = new ApiFrameCodec(options.MaxFrameLength);
        _outbox = new ApiOutbox(transport, options.OutgoingQueueCapacity, options.WriteTimeout, clock);
        _pending = new ApiPendingCalls(_outbox, options, clock);
        _dispatcher = new ApiDispatcher(this, registry, options, _outbox, executionSlots, clock, Abort);
        Completion = ObserveTransportAsync();
    }

    public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default) where TRequest : IApiRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        var operation = _registry.Get<TRequest, TResponse>();
        return (TResponse)await _pending.RequestAsync(operation, request, timeout, cancellationToken).ConfigureAwait(false);
    }

    public void Receive(ReadOnlyMemory<byte> frame)
    {
        if (Volatile.Read(ref _closing) != 0) { return; }
        try
        {
            var envelope = _codec.Decode(frame);
            if (envelope.Kind == ApiMessageKind.Request) { _dispatcher.TryDispatch(envelope); }
            else { CompletePending(envelope); }
        }
        catch (Exception exception) { Abort(exception); }
    }

    public Task DrainAsync()
    {
        lock (_gate)
        {
            if (_drain is not null) { return _drain; }
            _dispatcher.StopAdmission();
            _drain = DrainCoreAsync(_pending.StopAdmission());
            return _drain;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closing, 1) == 0)
        {
            var error = new IOException("The API connection closed.");
            _pending.FailAll(error);
            _dispatcher.CancelAll();
            _outbox.Abort(error);
            return _transport.CloseAsync(CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    private void CompletePending(ApiEnvelope envelope)
    {
        if (!_pending.IsPendingResponse(envelope.RequestId, envelope.OperationId))
        { Interlocked.Increment(ref _lateResponses); return; }
        ApiError? error = null;
        if (envelope.Kind == ApiMessageKind.Error)
        {
            var reader = new MessagePackReader(envelope.Payload);
            if (reader.ReadArrayHeader() != 2) { throw new ApiProtocolException("Invalid error payload shape."); }
            error = ApiPayloadSerializer.Deserialize<ApiError>(envelope.Payload);
            if (error.Code is < ApiErrorCode.UnsupportedOperation or > ApiErrorCode.InternalError || error.Message is null || error.Message.Length > 256)
            { throw new ApiProtocolException("Invalid API error payload."); }
        }
        if (!_pending.TryComplete(envelope.RequestId, envelope.OperationId, envelope.Payload, error)) { Interlocked.Increment(ref _lateResponses); }
    }

    private void Abort(Exception error)
    {
        Logger.Warning("API connection {ConnectionId} for peer {PeerId} closed: {ErrorType}", ConnectionId, Peer.PeerId, error.GetType().Name);
        _ = CloseAsync();
    }

    private async Task DrainCoreAsync(Task pendingDrained)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            await Task.WhenAll(_dispatcher.Completion, pendingDrained).ConfigureAwait(false);
            _outbox.Complete();
            await _outbox.Completion.ConfigureAwait(false);
        }
        catch (Exception exception) { Abort(exception); }
        await CloseAsync().ConfigureAwait(false);
        await Completion.ConfigureAwait(false);
    }

    private async Task ObserveTransportAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try { await _transport.Completion.ConfigureAwait(false); }
        finally
        {
            await CloseAsync().ConfigureAwait(false);
            await _dispatcher.Completion.ConfigureAwait(false);
            await _pending.Completion.ConfigureAwait(false);
            try { await _outbox.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception)
            {
                Logger.Debug("API writer stopped for connection {ConnectionId}: {ErrorType}", ConnectionId, exception.GetType().Name);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        await Completion.WaitAsync(_options.ShutdownTimeout, _clock).ConfigureAwait(false);
    }
}
