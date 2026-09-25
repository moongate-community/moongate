using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Server;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Integration.Client;

public sealed class MoongateTcpClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Completion_BeforeStart_IsStableAndWaitsForCleanup()
    {
        var stream = new GatedWriteStream { HoldReadAfterCancellation = true };
        var pair = await LoopbackPair.CreateAsync(stream, startSender: false);
        var completion = pair.Sender.Completion;

        try
        {
            Assert.False(completion.IsCompleted);
            await pair.Sender.StartAsync(CancellationToken.None);
            await stream.ReadEntered.Task.WaitAsync(Timeout);
            await pair.Sender.CloseAsync();
            Assert.Same(completion, pair.Sender.Completion);
            Assert.False(completion.IsCompleted);
            stream.ReleaseRead.TrySetResult();
            await completion.WaitAsync(Timeout);
            Assert.Equal(1, stream.DisposeCount);
        }
        finally
        {
            stream.ReleaseRead.TrySetResult();
            await pair.DisposeAsync();
        }
    }

    [Fact]
    public async Task Completion_NormalEof_DisconnectsOnceWithoutDiagnostic()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var disconnects = 0;
        var errors = 0;
        pair.Receiver.OnDisconnected += (_, _) => Interlocked.Increment(ref disconnects);
        pair.Receiver.OnException += (_, _) => Interlocked.Increment(ref errors);
        await pair.Sender.CloseAsync();
        await pair.Receiver.Completion.WaitAsync(Timeout);
        await pair.Receiver.DisposeAsync();
        Assert.Equal(1, disconnects);
        Assert.Equal(0, errors);
    }

    [Fact]
    public async Task ConnectAsync_RefusedConnection_PropagatesFailure()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;
        listener.Dispose();
        await Assert.ThrowsAsync<SocketException>(() => MoongateTcpClient.ConnectAsync(endpoint));
    }

    [Theory, InlineData(1, 1), InlineData(1024 * 1024, 16 * 1024 * 1024)]
    public async Task Constructor_ExactBufferLimitBoundaries_AcceptsConfiguration(
        int receiveBufferSize,
        int maxFrameLength
    )
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await using var client = new MoongateTcpClient(
            socket,
            Stream.Null,
            receiveBufferSize: receiveBufferSize,
            maxFrameLength: maxFrameLength
        );

        Assert.Equal(receiveBufferSize, client.ReceiveBufferSize);
    }

    [Theory, InlineData(0, 1, "receiveBufferSize"), InlineData(1024 * 1024 + 1, 1, "receiveBufferSize"),
     InlineData(1, 0, "maxFrameLength"), InlineData(1, 16 * 1024 * 1024 + 1, "maxFrameLength")]
    public void Constructor_OutOfRangeBufferLimits_RejectsConfiguration(
        int receiveBufferSize,
        int maxFrameLength,
        string parameterName
    )
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new MoongateTcpClient(
                socket,
                Stream.Null,
                receiveBufferSize: receiveBufferSize,
                maxFrameLength: maxFrameLength
            )
        );

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public async Task DiagnosticAndDisconnectSubscribers_Throw_RemainingSubscribersAndCleanupRun()
    {
        var stream = new GatedWriteStream { WriteFailure = new IOException("write failed") };
        await using var pair = await LoopbackPair.CreateAsync(stream);
        var errors = 0;
        var disconnects = 0;
        pair.Sender.OnException += (_, _) => throw new InvalidOperationException("diagnostic callback");
        pair.Sender.OnException += (_, _) => errors++;
        pair.Sender.OnDisconnected += (_, _) => throw new InvalidOperationException("disconnect callback");
        pair.Sender.OnDisconnected += (_, _) => disconnects++;
        var send = pair.Sender.SendAsync(new byte[] { 1 }, CancellationToken.None);
        await stream.WriteEntered.Task.WaitAsync(Timeout);
        stream.ReleaseWrite.TrySetResult();
        await Assert.ThrowsAsync<IOException>(() => send.WaitAsync(Timeout));
        await pair.Sender.Completion.WaitAsync(Timeout);
        Assert.Equal(1, errors);
        Assert.Equal(1, disconnects);
    }

    [Fact]
    public async Task DisposeAsync_CancellationCallbackAndStreamReleaseFail_AttemptsAllCleanup()
    {
        var cancellationFailure = new InvalidOperationException("cancellation failure");
        var releaseFailure = new IOException("release failure");
        var stream = new GatedWriteStream { CancellationFailure = cancellationFailure, DisposeFailure = releaseFailure };
        var pair = await LoopbackPair.CreateAsync(stream);
        await stream.ReadEntered.Task.WaitAsync(Timeout);
        var failures =
            await Assert.ThrowsAsync<AggregateException>(() => pair.Sender.DisposeAsync().AsTask().WaitAsync(Timeout));
        Assert.Contains(cancellationFailure, failures.Flatten().InnerExceptions);
        Assert.Contains(releaseFailure, failures.Flatten().InnerExceptions);
        Assert.Equal(1, stream.DisposeCount);
        Assert.Null(pair.Sender.LocalEndPoint);
        await pair.Receiver.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DuringStatefulMiddleware_WaitsForAllAdmittedSends()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var middleware = new GatedKeystreamMiddleware { IgnoreCancellationWhileHeld = true };
        pair.Sender.AddMiddleware(middleware);
        var first = pair.Sender.SendAsync(new byte[] { 1 }, CancellationToken.None);
        await middleware.KeystreamTaken.Task.WaitAsync(Timeout);
        var waiter = pair.Sender.SendAsync(new byte[] { 2 }, CancellationToken.None);
        var dispose = pair.Sender.DisposeAsync().AsTask();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter.WaitAsync(Timeout));
            Assert.False(dispose.IsCompleted);
            Assert.False(pair.Sender.Completion.IsCompleted);
        }
        finally
        {
            middleware.Release();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
        await dispose.WaitAsync(Timeout);
    }

    [Fact]
    public async Task DisposeAsync_InFlightWriteAndWaiters_DrainsBeforeReleasingOnce()
    {
        var stream = new GatedWriteStream();
        var pair = await LoopbackPair.CreateAsync(stream);
        var diagnostics = 0;
        pair.Sender.OnException += (_, _) => Interlocked.Increment(ref diagnostics);
        var send = pair.Sender.SendAsync(new byte[] { 1 }, CancellationToken.None);
        await stream.WriteEntered.Task.WaitAsync(Timeout);
        using var waiterCancellation = new CancellationTokenSource();
        var waiters = Enumerable.Range(0, 20)
            .Select(_ => pair.Sender.SendAsync(new byte[] { 2 }, waiterCancellation.Token))
            .ToArray();
        var firstDispose = pair.Sender.DisposeAsync().AsTask();
        var secondDispose = pair.Sender.DisposeAsync().AsTask();

        try
        {
            await Task.WhenAll(firstDispose, secondDispose).WaitAsync(Timeout);
            Assert.True(send.IsCompleted);
            Assert.All(waiters, waiter => Assert.True(waiter.IsCompleted));
            Assert.False(stream.DisposedDuringWrite);
            Assert.Equal(1, stream.DisposeCount);
            Assert.Equal(0, diagnostics);
        }
        finally
        {
            stream.ReleaseWrite.TrySetResult();
            waiterCancellation.Cancel();
            await Task.WhenAll(
                waiters.Append(send).Select(async task => await Record.ExceptionAsync(() => task.WaitAsync(Timeout)))
            );
            await pair.Receiver.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_ReleaseFails_ReattemptAwaitsSameFaultAndSocketStillCloses()
    {
        var failure = new IOException("dispose failed");
        var stream = new GatedWriteStream { DisposeFailure = failure };
        var pair = await LoopbackPair.CreateAsync(stream);
        Assert.Same(
            failure,
            await Assert.ThrowsAsync<IOException>(() => pair.Sender.DisposeAsync().AsTask().WaitAsync(Timeout))
        );
        Assert.Same(
            failure,
            await Assert.ThrowsAsync<IOException>(() => pair.Sender.DisposeAsync().AsTask().WaitAsync(Timeout))
        );
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => pair.Sender.Completion.WaitAsync(Timeout)));
        Assert.Equal(1, stream.DisposeCount);
        Assert.Null(pair.Sender.LocalEndPoint);
        await pair.Receiver.DisposeAsync();
    }

    [Fact]
    public async Task OnConnected_DisposeSynchronously_DoesNotReleaseBeforeCallbackReturns()
    {
        var stream = new GatedWriteStream();
        await using var pair = await LoopbackPair.CreateAsync(stream, startSender: false);
        var completionFinishedInsideCallback = true;
        var disposeCountInsideCallback = -1;
        pair.Sender.OnConnected += (_, _) =>
        {
            pair.Sender.Dispose();
            completionFinishedInsideCallback = pair.Sender.Completion.IsCompleted;
            disposeCountInsideCallback = stream.DisposeCount;
        };
        await pair.Sender.StartAsync(CancellationToken.None);
        await pair.Sender.Completion.WaitAsync(Timeout);
        Assert.False(completionFinishedInsideCallback);
        Assert.Equal(0, disposeCountInsideCallback);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task OnConnected_Throws_ClosesAndCompletesCleanup()
    {
        var stream = new GatedWriteStream();
        await using var pair = await LoopbackPair.CreateAsync(stream, startSender: false);
        var error = new InvalidOperationException("connect callback");
        Exception? diagnostic = null;
        pair.Sender.OnConnected += (_, _) => throw error;
        pair.Sender.OnException += (_, args) => diagnostic = args.Exception;
        await pair.Sender.StartAsync(CancellationToken.None);
        await pair.Sender.Completion.WaitAsync(Timeout);
        Assert.Same(error, diagnostic);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task OnDataReceived_Throws_ClosesOnlyAffectedConnection()
    {
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        var failed = new TaskCompletionSource<MoongateTcpClient>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.OnDataReceived += (_, args) =>
        {
            if (args.Data.Span[0] == 1)
            {
                failed.TrySetResult(args.Client);

                throw new InvalidOperationException("data callback");
            }

            received.TrySetResult(args.Data.ToArray());
        };
        await server.StartAsync(CancellationToken.None);
        await using var first = await MoongateTcpClient.ConnectAsync(new(IPAddress.Loopback, server.Port));
        await first.SendAsync(new byte[] { 1 }, CancellationToken.None);
        var failedClient = await failed.Task.WaitAsync(Timeout);
        await failedClient.Completion.WaitAsync(Timeout);
        await using var second = await MoongateTcpClient.ConnectAsync(new(IPAddress.Loopback, server.Port));
        await second.SendAsync(new byte[] { 2 }, CancellationToken.None);
        Assert.Equal(new byte[] { 2 }, await received.Task.WaitAsync(Timeout));
        Assert.True(server.IsRunning);
    }

    [Fact]
    public async Task ReceiveAsync_FramedMiddlewareExpansionBeyondPendingBudget_ClosesWithoutDispatch()
    {
        await using var pair = await LoopbackPair.CreateAsync(
            receiverMiddlewares: [new AppendingMiddleware(0xAA), new AppendingMiddleware(0xBB)],
            receiverFramer: new BogusLengthFramer(1),
            receiverBufferSize: 1,
            receiverMaxFrameLength: 1
        );
        var outcome = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        pair.Receiver.OnException += (_, args) => outcome.TrySetResult(args.Exception);
        pair.Receiver.OnDataReceived += (_, args) => outcome.TrySetResult(args.Data.ToArray());

        await pair.Sender.SendAsync(new byte[] { 0x01 }, CancellationToken.None);

        Assert.IsType<InvalidDataException>(await outcome.Task.WaitAsync(Timeout));
        await pair.Receiver.Completion.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ReceiveAsync_MultipleFramesWhoseCombinedLengthExceedsFrameCap_DeliversEveryFrame()
    {
        await using var pair = await LoopbackPair.CreateAsync(
            receiverFramer: new BogusLengthFramer(1),
            receiverBufferSize: 4,
            receiverMaxFrameLength: 1
        );
        var received = new List<byte>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pair.Receiver.OnDataReceived += (_, args) =>
        {
            received.Add(args.Data.Span[0]);

            if (received.Count == 4)
            {
                allReceived.TrySetResult();
            }
        };

        await pair.Sender.SendAsync(new byte[] { 1, 2, 3, 4 }, CancellationToken.None);
        await allReceived.Task.WaitAsync(Timeout);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, received);
        Assert.True(pair.Receiver.IsConnected);
    }

    [Fact]
    public async Task ReceiveAsync_RawEventsRemainValidAcrossReadBufferReuse()
    {
        await using var pair = await LoopbackPair.CreateAsync(receiverBufferSize: 1);
        var firstReceived = new TaskCompletionSource<ReadOnlyMemory<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var secondReceived = new TaskCompletionSource<ReadOnlyMemory<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var eventCount = 0;
        pair.Receiver.OnDataReceived += (_, args) =>
        {
            if (Interlocked.Increment(ref eventCount) == 1)
            {
                firstReceived.TrySetResult(args.Data);
            }
            else
            {
                secondReceived.TrySetResult(args.Data);
            }
        };

        await pair.Sender.SendAsync(new byte[] { 0x11 }, CancellationToken.None);
        var first = await firstReceived.Task.WaitAsync(Timeout);
        await pair.Sender.SendAsync(new byte[] { 0x22 }, CancellationToken.None);
        var second = await secondReceived.Task.WaitAsync(Timeout);

        Assert.Equal(new byte[] { 0x11 }, first.ToArray());
        Assert.Equal(new byte[] { 0x22 }, second.ToArray());
    }

    [Fact]
    public async Task ReceiveAsync_RawMiddlewareExpansionBeyondReceiveBudget_ClosesWithoutDispatch()
    {
        await using var pair = await LoopbackPair.CreateAsync(
            receiverMiddlewares: [new AppendingMiddleware(0xAA)],
            receiverBufferSize: 1
        );
        var outcome = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        pair.Receiver.OnException += (_, args) => outcome.TrySetResult(args.Exception);
        pair.Receiver.OnDataReceived += (_, args) => outcome.TrySetResult(args.Data.ToArray());

        await pair.Sender.SendAsync(new byte[] { 0x01 }, CancellationToken.None);

        Assert.IsType<InvalidDataException>(await outcome.Task.WaitAsync(Timeout));
        await pair.Receiver.Completion.WaitAsync(Timeout);
    }

    [Fact]
    public async Task SendAsync_AdmittedWaiterAfterFailure_DoesNotTransformBeforeCleanupCancellation()
    {
        var stream = new GatedWriteStream { WriteFailure = new IOException("first write failed") };
        await using var pair = await LoopbackPair.CreateAsync(stream);
        var middleware = new GatedKeystreamMiddleware();
        middleware.Release();
        pair.Sender.AddMiddleware(middleware);
        var cleanup = new CleanupExecutionContextGate();
        var first = cleanup.CaptureSend(
            pair.Sender,
            () => pair.Sender.SendAsync(new byte[] { 1 }, CancellationToken.None)
        );
        Task? second = null;

        try
        {
            await stream.WriteEntered.Task.WaitAsync(Timeout);
            second = pair.Sender.SendAsync(new byte[] { 2 }, CancellationToken.None);
            stream.ReleaseWrite.TrySetResult();
            await cleanup.Entered.WaitAsync(Timeout);
            await Assert.ThrowsAsync<IOException>(() => second.WaitAsync(Timeout));
            Assert.False(middleware.SecondEntered.Task.IsCompleted);
            Assert.Equal(1, stream.WriteCount);
        }
        finally
        {
            cleanup.Release();
            stream.ReleaseWrite.TrySetResult();
            await Record.ExceptionAsync(() => first.WaitAsync(Timeout));

            if (second is not null)
            {
                await Record.ExceptionAsync(() => second.WaitAsync(Timeout));
            }

            await pair.Sender.Completion.WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task SendAsync_CancelDuringWrite_ClosesAndPreventsFurtherWrites()
    {
        var stream = new GatedWriteStream();
        await using var pair = await LoopbackPair.CreateAsync(stream);
        using var cancellation = new CancellationTokenSource();
        var send = pair.Sender.SendAsync(new byte[] { 1 }, cancellation.Token);
        await stream.WriteEntered.Task.WaitAsync(Timeout);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send.WaitAsync(Timeout));
        Assert.False(pair.Sender.IsConnected);
        await Assert.ThrowsAsync<IOException>(() => pair.Sender.SendAsync(new byte[] { 2 }, CancellationToken.None));
        Assert.Equal(1, stream.WriteCount);
    }

    [Fact]
    public async Task SendAsync_CancelWaitingForGate_LeavesConnectionUsable()
    {
        var stream = new GatedWriteStream();
        await using var pair = await LoopbackPair.CreateAsync(stream);
        var first = pair.Sender.SendAsync(new byte[] { 1 }, CancellationToken.None);
        await stream.WriteEntered.Task.WaitAsync(Timeout);
        using var cancellation = new CancellationTokenSource();
        var second = pair.Sender.SendAsync(new byte[] { 2 }, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WaitAsync(Timeout));
        stream.ReleaseWrite.TrySetResult();
        await first.WaitAsync(Timeout);
        Assert.True(pair.Sender.IsConnected);
        await pair.Sender.SendAsync(new byte[] { 3 }, CancellationToken.None);
    }

    [Fact]
    public async Task SendAsync_ClosedConnection_ReportsFailureToCaller()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        await pair.Receiver.CloseAsync();
        await Assert.ThrowsAsync<IOException>(() => pair.Receiver.SendAsync(new byte[] { 0x42 }, CancellationToken.None));
        await Assert.ThrowsAsync<IOException>(() => pair.Receiver.SendAsync(
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task SendAsync_CodecEnabled_PreservesCallerPayloadAndWritesEncodedBytes()
    {
        await using var pair = await LoopbackPair.CreateAsync(codec: new CountingXorCodec(0x10));
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bytes = new List<byte>();
        pair.Receiver.OnDataReceived += (_, args) =>
        {
            bytes.AddRange(args.Data.ToArray());

            if (bytes.Count == 3)
            {
                received.TrySetResult(bytes.ToArray());
            }
        };
        var payload = new byte[] { 1, 2, 3 };
        await pair.Sender.SendAsync(payload, CancellationToken.None);
        Assert.Equal(new byte[] { 1, 2, 3 }, payload);
        Assert.Equal(new byte[] { 0x11, 0x13, 0x11 }, await received.Task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task SendAsync_MiddlewareFails_PropagatesAndCloses()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var error = new InvalidOperationException("middleware failure");
        var middleware = new GatedKeystreamMiddleware { SendFailure = error };
        pair.Sender.AddMiddleware(middleware);
        var send = pair.Sender.SendAsync(new byte[] { 1 }, CancellationToken.None);
        await middleware.KeystreamTaken.Task.WaitAsync(Timeout);
        middleware.Release();
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => send.WaitAsync(Timeout)));
        Assert.False(pair.Sender.IsConnected);
    }

    [Fact]
    public async Task SendAsync_WriteFails_PropagatesSameExceptionAndCloses()
    {
        var error = new IOException("write failed");
        var stream = new GatedWriteStream { WriteFailure = error };
        await using var pair = await LoopbackPair.CreateAsync(stream);
        var send = pair.Sender.SendAsync(new byte[] { 1 }, CancellationToken.None);
        await stream.WriteEntered.Task.WaitAsync(Timeout);
        stream.ReleaseWrite.TrySetResult();
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => send.WaitAsync(Timeout)));
        Assert.False(pair.Sender.IsConnected);
    }

    [Fact]
    public async Task StartAsync_ClosedClient_CannotRestart()
    {
        await using var pair = await LoopbackPair.CreateAsync(startSender: false);
        await pair.Sender.CloseAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pair.Sender.StartAsync(CancellationToken.None));
    }
}
