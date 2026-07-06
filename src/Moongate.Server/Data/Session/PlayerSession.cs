using Moongate.Network.Interfaces;
using Moongate.Network.Middlewares;
using Moongate.Server.Types;
using SquidStd.Network.Client;
using SquidStd.Network.Spans;

namespace Moongate.Server.Data.Session;

/// <summary>Server-side state for one connected client: protocol phase, seed, account, compression.</summary>
public sealed class PlayerSession
{
    private readonly SquidStdTcpClient _client;
    private readonly object _stateSync = new();

    public long SessionId { get; }

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

    public void MarkAuthenticated(string username)
    {
        lock (_stateSync)
        {
            Username = username;
            State = SessionStateType.Authenticated;
        }
    }

    public Task SendAsync<TPacket>(TPacket packet) where TPacket : IOutgoingPacket
    {
        var writer = new SpanWriter(1024, resize: true);
        packet.Write(ref writer);
        var bytes = writer.Span.ToArray();

        return _client.SendAsync(bytes, CancellationToken.None);
    }
}
