using Moongate.Api.Connections.Internal;
using Moongate.Api.Data.Errors;
using Moongate.Api.Exceptions;
using Moongate.Api.Registry;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Tests.TestSupport.Connections;
using Moongate.Api.Tests.TestSupport.Contracts;
using Moongate.Api.Types.Protocol;

namespace Moongate.Api.Tests.Connections;

public class ApiConnectionProtocolTests
{
    [Fact]
    public async Task LateCancelledResponse_IsIgnoredWithoutDecodingPayload()
    {
        var transport = new RecordingConnection();
        using var slots = new SemaphoreSlim(1);
        await using var connection = Create(transport, slots);
        using var cancellation = new CancellationTokenSource();
        var call = connection.RequestAsync<IncrementRequest, IncrementResponse>(
            new(),
            cancellationToken: cancellation.Token
        );
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        connection.Receive(new ApiFrameCodec(65536).Encode(new(ApiMessageKind.Response, 1, 100, new byte[] { 0xc1 })));
        Assert.Equal(1, connection.LateResponseCount);
        Assert.True(transport.IsConnected);
    }

    [Theory, InlineData("id"), InlineData("operation"), InlineData("payload"), InlineData("error")]
    public async Task MalformedResponse_ClosesConnectionAndFailsPendingCall(string failure)
    {
        var transport = new RecordingConnection();
        using var slots = new SemaphoreSlim(1);
        await using var connection = Create(transport, slots);
        var call = connection.RequestAsync<IncrementRequest, IncrementResponse>(new());
        var kind = failure == "error" ? ApiMessageKind.Error : ApiMessageKind.Response;
        var payload = failure == "error" ? ApiPayloadSerializer.Serialize(
                          new ApiError { Code = (ApiErrorCode)99, Message = "Unknown" },
                          100
                      ) :
                      failure == "payload" ? new byte[] { 0xc0 } : new byte[] { 0x91, 42 };
        var frame = new ApiFrameCodec(65536).Encode(
            new(kind, failure == "id" ? 2u : 1u, failure == "operation" ? (ushort)101 : (ushort)100, payload)
        );
        connection.Receive(frame);
        await Assert.ThrowsAnyAsync<IOException>(() => call);
        await connection.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(transport.IsConnected);
    }

    [Fact]
    public async Task RemoteError_IsDistinctFromLocalTimeoutOrDisconnect()
    {
        var transport = new RecordingConnection();
        using var slots = new SemaphoreSlim(1);
        await using var connection = Create(transport, slots);
        var call = connection.RequestAsync<IncrementRequest, IncrementResponse>(new());
        var payload = ApiPayloadSerializer.Serialize(
            new ApiError { Code = ApiErrorCode.Forbidden, Message = "Forbidden" },
            100
        );
        connection.Receive(new ApiFrameCodec(65536).Encode(new(ApiMessageKind.Error, 1, 100, payload)));
        Assert.Equal(ApiErrorCode.Forbidden, (await Assert.ThrowsAsync<ApiRemoteException>(() => call)).Code);
        Assert.True(transport.IsConnected);
    }

    private static ApiConnection Create(RecordingConnection transport, SemaphoreSlim slots)
    {
        var registry = new ApiRegistry();
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        registry.Freeze();

        return new(
            transport,
            new("peer", [100]),
            registry,
            new(),
            slots,
            TimeProvider.System
        );
    }
}
