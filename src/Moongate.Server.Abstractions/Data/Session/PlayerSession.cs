using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Network.Middlewares;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Internal;
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

    /// <summary>Smallest view range a client may ask for, in tiles.</summary>
    public const int MinViewRange = 5;

    /// <summary>
    /// Largest view range a client may ask for, in tiles, and the radius the server broadcasts to.
    /// </summary>
    public const int MaxViewRange = 18;

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
    /// The update range this client asked for via 0xC8, in tiles, always within
    /// <see cref="MinViewRange" />..<see cref="MaxViewRange" />. Starts at the maximum: the client
    /// sees everything until it says otherwise.
    /// </summary>
    public int ViewRange { get; private set; } = MaxViewRange;

    /// <summary>
    /// The item this client is dragging on its cursor, or <see cref="Serial.Zero" /> when its hands
    /// are empty. Deliberately not persisted: a held item is attached to nothing, and session state
    /// cannot outlive the process, so a crash mid-drag cannot strand it.
    /// </summary>
    public Serial HeldItemId { get; private set; }

    /// <summary>Where <see cref="HeldItemId" /> came from, so a failed drop can bounce it back.</summary>
    public HeldItemOrigin? HeldItemOrigin { get; private set; }

    /// <summary>
    /// The last movement sequence number accepted from this client, or null before the first accepted move (or after a
    /// resync).
    /// </summary>
    public byte? LastMoveSequence { get; private set; }

    /// <summary>When the last accepted move was recorded — the baseline the walk/run rate limit measures against.</summary>
    public DateTimeOffset LastMoveAt { get; private set; }

    /// <summary>
    /// The earliest this client may take its next step. A step arriving before it is early, and is
    /// paid for out of <see cref="MovementCredit" /> rather than refused outright.
    /// </summary>
    public DateTimeOffset NextMoveAt { get; private set; }

    /// <summary>
    /// Slack for early steps, in the ModernUO sense: network jitter routinely delivers a packet a few
    /// milliseconds ahead of schedule, and refusing those is what makes movement stutter. Arriving
    /// late rebuilds it, up to the configured ceiling; arriving early spends it, down to the matching
    /// debt limit. Past that the step waits in <see cref="MovementQueue" /> instead.
    /// </summary>
    public TimeSpan MovementCredit { get; private set; }

    /// <summary>
    /// Steps that arrived too early to run and too soon to refuse, in the order they came. Drained on
    /// the game loop as each becomes due. Empty for a client walking at a normal pace.
    /// </summary>
    public Queue<QueuedMove> MovementQueue { get; } = new();

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

    /// <summary>
    /// Holds a requested view range between <see cref="MinViewRange" /> and
    /// <see cref="MaxViewRange" />. Static and pure so the rule can be tested without a live
    /// session, which needs a socket.
    /// </summary>
    public static int ClampViewRange(int requested)
        => Math.Clamp(requested, MinViewRange, MaxViewRange);

    /// <summary>Empties the client's hands, after a successful drop or a bounce.</summary>
    public void ClearHold()
    {
        lock (_stateSync)
        {
            HeldItemId = Serial.Zero;
            HeldItemOrigin = null;
        }
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

    /// <summary>Records the item now on the client's cursor and where it was lifted from.</summary>
    public void Hold(Serial itemId, HeldItemOrigin origin)
    {
        lock (_stateSync)
        {
            HeldItemId = itemId;
            HeldItemOrigin = origin;
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

    /// <summary>Sets when the next step becomes due, after a step has been taken.</summary>
    public void SetNextMoveAt(DateTimeOffset at)
    {
        lock (_stateSync)
        {
            NextMoveAt = at;
        }
    }

    /// <summary>Spends or rebuilds the jitter slack. Clamping is the caller's, which knows the ceiling.</summary>
    public void SetMovementCredit(TimeSpan credit)
    {
        lock (_stateSync)
        {
            MovementCredit = credit;
        }
    }

    /// <summary>
    /// Forgets every pending step and the slack with them. A refusal resyncs the client to the
    /// server's position, so anything still queued describes a walk that no longer happened.
    /// </summary>
    public void ClearMovementQueue()
    {
        lock (_stateSync)
        {
            MovementQueue.Clear();
            MovementCredit = TimeSpan.Zero;
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

    /// <summary>Records the view range reported via 0xC8, clamped to the range the server allows.</summary>
    public void SetViewRange(int range)
    {
        lock (_stateSync)
        {
            ViewRange = ClampViewRange(range);
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
