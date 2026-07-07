using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Network.Middlewares;
using Moongate.Server.Interfaces;
using Moongate.Server.Types;
using Serilog;
using SquidStd.Network.Client;
using SquidStd.Network.Spans;

namespace Moongate.Server.Data.Session;

/// <summary>Server-side state for one connected client: protocol phase, seed, account, compression.</summary>
public sealed class PlayerSession : ISeedTarget
{
    private const int InitialWriteBufferSize = 1024;

    private readonly ILogger _logger = Log.ForContext<PlayerSession>();
    private readonly SquidStdTcpClient _client;
    private readonly Lock _stateSync = new();

    public long SessionId { get; }

    public Serial AccountId { get; private set; }

    public SessionStateType State { get; private set; }

    public uint? Seed { get; private set; }

    public string? Username { get; private set; }

    public UoCompressionMiddleware Compression { get; }

    public PlayerSession(SquidStdTcpClient client)
    {
        _client = client;
        SessionId = client.SessionId;
        State = SessionStateType.AwaitingSeed;
        Compression = new UoCompressionMiddleware();
        client.AddMiddleware(Compression);
    }

    public void SetState(SessionStateType state)
    {
        lock (_stateSync)
        {
            State = state;
        }
    }

    public void SetSeed(uint seed)
    {
        lock (_stateSync)
        {
            Seed = seed;
        }
    }

    public void SetAccountId(Serial accountId)
    {
        lock (_stateSync)
        {
            AccountId = accountId;
        }
    }

    public void MarkAuthenticated(string username)
    {
        lock (_stateSync)
        {
            Username = username;
            State = SessionStateType.Authenticated;
        }
    }

    /// <summary>
    /// Serializes <paramref name="packet" /> on the calling (main game-loop) thread and hands the
    /// bytes to the client fire-and-forget, so the socket I/O never blocks the frame. The client's
    /// internal send lock keeps writes ordered per session.
    /// </summary>
    public void Send<TPacket>(TPacket packet) where TPacket : IOutgoingPacket
    {
        var writer = new SpanWriter(InitialWriteBufferSize, resize: true);
        packet.Write(ref writer);
        var bytes = writer.Span.ToArray();

        _ = SendInternalAsync(bytes);
    }

    private async Task SendInternalAsync(byte[] bytes)
    {
        try
        {
            await _client.SendAsync(bytes, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Send failed on session {SessionId}", SessionId);
        }
    }
}
