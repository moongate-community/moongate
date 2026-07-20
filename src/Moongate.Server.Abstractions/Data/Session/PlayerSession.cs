using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Network.Middlewares;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Abstractions.Types;
using Moongate.UO.Data.Version;
using Serilog;
using SquidStd.Network.Client;
using SquidStd.Network.Spans;

namespace Moongate.Server.Abstractions.Data.Session;

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

    public ClientVersion Version { get; private set; }

    public uint? Seed { get; private set; }

    public string? Username { get; private set; }

    public MobileEntity? Character { get; private set; }

    public int ScreenWidth { get; private set; }

    public int ScreenHeight { get; private set; }

    public string? Language { get; private set; }

    /// <summary>
    /// The last movement sequence number accepted from this client, or null before the first accepted move (or after a
    /// resync).
    /// </summary>
    public byte? LastMoveSequence { get; private set; }

    /// <summary>When the last accepted move was recorded — the baseline the walk/run rate limit measures against.</summary>
    public DateTimeOffset LastMoveAt { get; private set; }

    /// <summary>When the last accepted speech packet was recorded — the baseline the chat rate limit measures against.</summary>
    public DateTimeOffset LastChatAt { get; private set; }

    public UoCompressionMiddleware Compression { get; }

    public PlayerSession(SquidStdTcpClient client)
    {
        _client = client;
        SessionId = client.SessionId;
        State = SessionStateType.AwaitingSeed;
        Compression = new();
        client.AddMiddleware(Compression);
    }

    /// <summary>Closes the underlying connection, dropping this session (fire-and-forget).</summary>
    public void Disconnect()
        => _ = _client.CloseAsync();

    /// <summary>
    /// Turns on UO transport compression for outbound packets. The login handshake is sent in the
    /// clear; the game server compresses everything from the character list onward.
    /// </summary>
    public void EnableCompression()
        => Compression.Enabled = true;

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
        var writer = new SpanWriter(InitialWriteBufferSize, true);
        packet.Write(ref writer);
        var bytes = writer.Span.ToArray();

        _ = SendInternalAsync(bytes);
    }

    public void SetAccountId(Serial accountId)
    {
        lock (_stateSync)
        {
            AccountId = accountId;
        }
    }

    /// <summary>Attaches the freshly created (or selected) character to this session.</summary>
    public void SetCharacter(MobileEntity character)
    {
        lock (_stateSync)
        {
            Character = character;
        }
    }

    /// <summary>Records the client language (e.g. "ENU") reported via 0xBF sub-command 0x0B.</summary>
    public void SetLanguage(string language)
    {
        lock (_stateSync)
        {
            Language = language;
        }
    }

    /// <summary>Records when the last accepted speech packet arrived, for the chat rate limit.</summary>
    public void SetLastChat(DateTimeOffset at)
    {
        lock (_stateSync)
        {
            LastChatAt = at;
        }
    }

    /// <summary>
    /// Records the outcome of a movement rate-limit check: the accepted sequence (or null to force a
    /// resync, so the next packet's sequence is accepted unconditionally) and when it happened.
    /// </summary>
    public void SetLastMove(byte? sequence, DateTimeOffset at)
    {
        lock (_stateSync)
        {
            LastMoveSequence = sequence;
            LastMoveAt = at;
        }
    }

    /// <summary>Records the client viewport size reported via 0xBF sub-command 0x05.</summary>
    public void SetScreenSize(int width, int height)
    {
        lock (_stateSync)
        {
            ScreenWidth = width;
            ScreenHeight = height;
        }
    }

    public void SetSeed(uint seed)
    {
        lock (_stateSync)
        {
            Seed = seed;
        }
    }

    public void SetState(SessionStateType state)
    {
        lock (_stateSync)
        {
            State = state;
        }
    }

    public void SetVersion(ClientVersion version)
    {
        lock (_stateSync)
        {
            Version = version;
        }
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
