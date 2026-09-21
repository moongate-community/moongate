using Microsoft.Extensions.Time.Testing;
using Moongate.Api.Connections.Internal;
using Moongate.Api.Data.Errors;
using Moongate.Api.Data.Internal.Protocol;
using Moongate.Api.Dispatch.Internal;
using Moongate.Api.Exceptions;
using Moongate.Api.Registry;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Tests.TestSupport.Connections;
using Moongate.Api.Tests.TestSupport.Contracts;
using Moongate.Api.Tests.TestSupport.Handlers;
using Moongate.Api.Types.Protocol;

namespace Moongate.Api.Tests.Dispatch;

public class ApiDispatcherTests
{
    [Theory, InlineData(101, true, true, ApiErrorCode.UnsupportedOperation),
     InlineData(100, false, true, ApiErrorCode.UnsupportedOperation), InlineData(100, true, false, ApiErrorCode.Forbidden),
     InlineData(100, true, true, ApiErrorCode.InvalidRequest)]
    public async Task Admission_RejectsUnknownMissingUnauthorizedOrMalformedRequests(
        ushort operation,
        bool hasHandler,
        bool allowed,
        ApiErrorCode expected
    )
    {
        var registry = new ApiRegistry();
        var handler = new GatedHandler();

        if (hasHandler)
        {
            registry.RegisterHandler(() => handler);
        }
        else
        {
            registry.RegisterContract<IncrementRequest, IncrementResponse>();
        }

        registry.Freeze();
        var transport = new RecordingConnection();
        await using var outbox = Outbox(transport);
        using var slots = new SemaphoreSlim(1);
        var dispatcher = new ApiDispatcher(
            new StubApiConnection(allowed ? [100] : []),
            registry,
            new(),
            outbox,
            slots,
            TimeProvider.System,
            error => Assert.Fail(error.Message)
        );
        var payload = allowed ? new byte[] { 0x91, 0xc2 } : new byte[] { 0xc1 };
        dispatcher.TryDispatch(new(ApiMessageKind.Request, 1, operation, payload));
        Assert.Equal(expected, ApiPayloadSerializer.Deserialize<ApiError>((await ReadAsync(transport)).Payload).Code);
        Assert.Equal(0, handler.Count);
        dispatcher.StopAdmission();
        await dispatcher.Completion;
    }

    [Fact]
    public async Task DeadlineWhileWaitingForGlobalPermit_NeverInvokesHandler()
    {
        var handler = new GatedHandler();
        var clock = new FakeTimeProvider();
        var transport = new RecordingConnection();
        await using var outbox = Outbox(transport);
        using var slots = new SemaphoreSlim(0, 1);
        var dispatcher = new ApiDispatcher(
            new StubApiConnection(100),
            Registry(handler),
            new(),
            outbox,
            slots,
            clock,
            error => Assert.Fail(error.Message)
        );
        dispatcher.TryDispatch(Request(1));
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(
            ApiErrorCode.DeadlineExceeded,
            ApiPayloadSerializer.Deserialize<ApiError>((await ReadAsync(transport)).Payload).Code
        );
        dispatcher.StopAdmission();
        await dispatcher.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, handler.Count);
        Assert.Equal(0, slots.CurrentCount);
    }

    [Fact]
    public async Task DecoderDepthLimit_AbortsBeforeHandlerInvocation()
    {
        var transport = new RecordingConnection();
        await using var outbox = Outbox(transport);
        var handler = new GatedHandler();
        var aborted = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var slots = new SemaphoreSlim(1);
        var dispatcher = new ApiDispatcher(
            new StubApiConnection(100),
            Registry(handler),
            new(),
            outbox,
            slots,
            TimeProvider.System,
            error => aborted.TrySetResult(error)
        );
        dispatcher.TryDispatch(
            new(ApiMessageKind.Request, 1, 100, Enumerable.Repeat((byte)0x91, 65).Append((byte)0xc0).ToArray())
        );
        Assert.IsType<ApiProtocolException>(await aborted.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await dispatcher.Completion;
        Assert.Equal(0, handler.Count);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task DuplicateRequestIdentifier_IsProtocolFailure()
    {
        var transport = new RecordingConnection();
        await using var outbox = Outbox(transport);
        using var slots = new SemaphoreSlim(1);
        var handler = new GatedHandler();
        handler.Release.SetResult();
        var dispatcher = new ApiDispatcher(
            new StubApiConnection(100),
            Registry(handler),
            new(),
            outbox,
            slots,
            TimeProvider.System,
            _ => { }
        );
        dispatcher.TryDispatch(Request(1));
        Assert.Throws<ApiProtocolException>(() => dispatcher.TryDispatch(Request(1)));
        dispatcher.StopAdmission();
        await dispatcher.Completion;
    }

    [Fact]
    public async Task FullResponseQueue_AbortsInsteadOfDroppingRejection()
    {
        var transport = new RecordingConnection { SendGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var outbox = new ApiOutbox(transport, 1, TimeSpan.FromSeconds(5), TimeProvider.System);
        outbox.TryEnqueue(new([1], null));
        await transport.SendEntered.Task;
        outbox.TryEnqueue(new([2], null));
        var aborted = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var slots = new SemaphoreSlim(1);
        var dispatcher = new ApiDispatcher(
            new StubApiConnection(),
            Registry(new()),
            new(),
            outbox,
            slots,
            TimeProvider.System,
            error => aborted.TrySetResult(error)
        );
        dispatcher.TryDispatch(Request(1));
        Assert.IsType<IOException>(await aborted.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await dispatcher.Completion;
        transport.SendGate.SetResult();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task NonCooperativeHandler_KeepsPermitAfterDeadlineAndSendsOneTerminalResponse(bool faultLate)
    {
        var handler = new GatedHandler(false, faultLate);
        var registry = Registry(handler);
        var clock = new FakeTimeProvider();
        var transport = new RecordingConnection();
        await using var outbox = Outbox(transport);
        using var slots = new SemaphoreSlim(1);
        var dispatcher = new ApiDispatcher(
            new StubApiConnection(100),
            registry,
            new(),
            outbox,
            slots,
            clock,
            error => Assert.Fail(error.Message)
        );
        Assert.True(dispatcher.TryDispatch(Request(1)));
        Assert.Equal(1, await handler.Entered.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        clock.Advance(TimeSpan.FromSeconds(5));
        var expired = await ReadAsync(transport);
        Assert.Equal(ApiErrorCode.DeadlineExceeded, ApiPayloadSerializer.Deserialize<ApiError>(expired.Payload).Code);
        Assert.Equal(0, slots.CurrentCount);
        Assert.True(dispatcher.TryDispatch(Request(2)));
        Assert.Equal(1, handler.Count);
        handler.Release.SetResult();
        Assert.Equal(2, await handler.Entered.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        var next = await ReadAsync(transport);
        Assert.Equal(2u, next.RequestId);
        dispatcher.StopAdmission();
        await dispatcher.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, slots.CurrentCount);
        Assert.Equal(2, transport.Sent.Count);
    }

    [Fact]
    public async Task QueueFull_ReturnsBusyAndQueuedRequestExpiresWithoutRunning()
    {
        var handler = new GatedHandler(false);
        var clock = new FakeTimeProvider();
        var transport = new RecordingConnection();
        await using var outbox = Outbox(transport);
        using var slots = new SemaphoreSlim(1);
        var dispatcher = new ApiDispatcher(
            new StubApiConnection(100),
            Registry(handler),
            new() { IncomingQueueCapacity = 1 },
            outbox,
            slots,
            clock,
            error => Assert.Fail(error.Message)
        );
        dispatcher.TryDispatch(Request(1));
        await handler.Entered.Reader.ReadAsync();
        Assert.True(dispatcher.TryDispatch(Request(2)));
        Assert.False(dispatcher.TryDispatch(Request(3)));
        Assert.Equal(
            ApiErrorCode.Busy,
            ApiPayloadSerializer.Deserialize<ApiError>((await ReadAsync(transport)).Payload).Code
        );
        clock.Advance(TimeSpan.FromSeconds(5));
        await ReadAsync(transport);
        await ReadAsync(transport);
        handler.Release.SetResult();
        dispatcher.StopAdmission();
        await dispatcher.Completion;
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task SeparateConnections_ShareTheHostExecutionLimit()
    {
        var handler = new GatedHandler();
        var registry = Registry(handler);
        var firstTransport = new RecordingConnection();
        var secondTransport = new RecordingConnection();
        await using var firstOutbox = Outbox(firstTransport);
        await using var secondOutbox = Outbox(secondTransport);
        using var slots = new SemaphoreSlim(1);
        var first = new ApiDispatcher(
            new StubApiConnection(100),
            registry,
            new(),
            firstOutbox,
            slots,
            TimeProvider.System,
            error => Assert.Fail(error.Message)
        );
        var second = new ApiDispatcher(
            new StubApiConnection(100),
            registry,
            new(),
            secondOutbox,
            slots,
            TimeProvider.System,
            error => Assert.Fail(error.Message)
        );
        first.TryDispatch(Request(1));
        await handler.Entered.Reader.ReadAsync();
        second.TryDispatch(Request(1));
        Assert.Equal(0, slots.CurrentCount);
        Assert.Equal(1, handler.Count);
        handler.Release.SetResult();
        await ReadAsync(firstTransport);
        await ReadAsync(secondTransport);
        first.StopAdmission();
        second.StopAdmission();
        await Task.WhenAll(first.Completion, second.Completion);
        Assert.Equal(2, handler.Count);
        Assert.Equal(1, slots.CurrentCount);
    }

    private static ApiOutbox Outbox(RecordingConnection transport)
        => new(
            transport,
            32,
            TimeSpan.FromSeconds(5),
            TimeProvider.System
        );

    private static async Task<ApiEnvelope> ReadAsync(RecordingConnection transport)
        => new ApiFrameCodec(65536).Decode(
            await transport.Written.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))
        );

    private static ApiRegistry Registry(GatedHandler handler)
    {
        var registry = new ApiRegistry();
        registry.RegisterHandler(() => handler);
        registry.Freeze();

        return registry;
    }

    private static ApiEnvelope Request(uint id)
        => new(ApiMessageKind.Request, id, 100, new byte[] { 0x91, 41 });
}
