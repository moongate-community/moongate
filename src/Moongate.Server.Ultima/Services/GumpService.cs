using System.Text;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Internal.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Gumps;
using Moongate.Server.Ultima.Types.Gumps;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the gumps open on each session, as ModernUO does: an answer is matched by serial and type id, checked against
///     what the gump offered, and handed to the gump once.
/// </summary>
public sealed class GumpService : IGumpService
{
    public const int MaxOpenGumps = 64;
    public const int MaxTextLength = 239;

    // The paperdoll's virtue gump answers with this type id unasked.
    private const uint VirtueGumpTypeId = 0x1CD;
    private const uint ScriptGumpTypeBit = 0x80000000u;

    // 5.0.0a, the first client that reads 0xDD; a client that has not told its version yet is taken as a modern one.
    private static readonly ClientVersion Compressed = new(5, 0, 0, 1);

    private readonly ILogger _logger = Log.ForContext<GumpService>();
    private readonly IPacketSendService _sender;

    private uint _lastSerial;

    public GumpService(IPacketSendService sender)
    {
        _sender = sender;
    }

    /// <summary>
    ///     Gets the type id of a gump id: FNV-1a of its UTF-8 bytes, the same at every start, never 0 or the virtue gump's.
    /// </summary>
    public static uint TypeIdOf(string id)
    {
        var hash = 2166136261u;

        foreach (var value in Encoding.UTF8.GetBytes(id))
        {
            hash = (hash ^ value) * 16777619u;
        }

        return hash is 0 or VirtueGumpTypeId ? hash ^ ScriptGumpTypeBit : hash;
    }

    public void Open(GameSession session, GumpInstance gump)
    {
        var state = State(session);

        if (state.Closing)
        {
            _logger.Debug("Gump {Gump} not opened: session {SessionId} is closing", gump.Id, session.SessionId);

            return;
        }

        var typeId = TypeIdOf(gump.Id);
        var serial = ++_lastSerial == 0 ? ++_lastSerial : _lastSerial;
        var built = gump.Layout.Build();

        // Built first: a gump too large to send leaves nothing behind.
        var version = session.NetworkSession.ClientVersion;
        IOutgoingPacket packet = version is null || version >= Compressed
            ? new CompressedGumpPacket(serial, typeId, gump.X, gump.Y, built)
            : new GumpPacket(serial, typeId, gump.X, gump.Y, built);

        var closed = new List<(OpenGump Gump, GumpCloseReasonType Reason)>();

        if (state.Open.FindIndex(open => open.TypeId == typeId) is var same and >= 0)
        {
            closed.Add((Take(session, state, same), GumpCloseReasonType.Replaced));
        }

        if (state.Open.Count >= MaxOpenGumps)
        {
            closed.Add((Take(session, state, 0), GumpCloseReasonType.Server));
        }

        state.Open.Add(new() { Serial = serial, TypeId = typeId, Gump = gump, Built = built });
        _sender.TrySend(session.SessionId, packet);

        // Told last, so a gump they open again replaces this one instead of sitting beside it.
        foreach (var (open, reason) in closed)
        {
            Closed(session, open, reason);
        }
    }

    public bool Close(GameSession session, string id)
    {
        var state = session.Get(GumpSessionKeys.State);
        var typeId = TypeIdOf(id);
        var index = state?.Open.FindIndex(open => open.TypeId == typeId) ?? -1;

        if (state is null || index < 0)
        {
            return false;
        }

        Closed(session, Take(session, state, index), GumpCloseReasonType.Server);

        return true;
    }

    public void Respond(GameSession session, GumpResponsePacket packet)
    {
        var state = session.Get(GumpSessionKeys.State);
        var index = state?.Open.FindIndex(open => open.Serial == packet.Serial && open.TypeId == packet.TypeId) ?? -1;

        if (state is null || index < 0)
        {
            _logger.Debug(
                "Session {SessionId} answered gump {Serial}/{TypeId}, which is not open",
                session.SessionId,
                packet.Serial,
                packet.TypeId
            );

            return;
        }

        var open = state.Open[index];
        state.Open.RemoveAt(index);

        if (Invalid(open.Built, packet) is { } reason)
        {
            _logger.Warning(
                "Session {SessionId} answered gump {Gump} with {Reason}: dropped",
                session.SessionId,
                open.Gump.Id,
                reason
            );

            return;
        }

        var response = new GumpResponse
        {
            ButtonId = packet.ButtonId, Switches = packet.Switches.ToHashSet(),
            Texts = packet.TextEntries.ToDictionary(entry => entry.Id, entry => entry.Text)
        };

        try
        {
            open.Gump.OnResponse(session, response);
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Gump {Gump} failed to handle the answer of session {SessionId}",
                open.Gump.Id,
                session.SessionId
            );
        }
    }

    public void OnSessionClosed(GameSession session)
    {
        if (session.Get(GumpSessionKeys.State) is not { } state)
        {
            return;
        }

        state.Closing = true;
        var open = state.Open.ToList();
        state.Open.Clear();

        foreach (var gump in open)
        {
            Closed(session, gump, GumpCloseReasonType.Disconnect);
        }
    }

    private static string? Invalid(GumpBuildResult built, GumpResponsePacket packet)
    {
        if (packet.ButtonId != 0 && !built.Buttons.Contains(packet.ButtonId))
        {
            return $"button {packet.ButtonId}, which it does not have";
        }

        foreach (var id in packet.Switches)
        {
            if (!built.Switches.Contains(id))
            {
                return $"switch {id}, which it does not have";
            }
        }

        if (packet.TextEntries.DistinctBy(entry => entry.Id).Count() != packet.TextEntries.Count)
        {
            return "the same text entry twice";
        }

        foreach (var (id, text) in packet.TextEntries)
        {
            if (!built.TextEntries.Contains(id))
            {
                return $"text entry {id}, which it does not have";
            }

            if (text.Length > MaxTextLength)
            {
                return $"a text of {text.Length} characters";
            }
        }

        return null;
    }

    // Takes the gump off the session and off the client, without telling it yet.
    private OpenGump Take(GameSession session, GumpState state, int index)
    {
        var open = state.Open[index];
        state.Open.RemoveAt(index);
        _sender.TrySend(session.SessionId, new CloseGumpPacket(open.TypeId, 0));

        return open;
    }

    private void Closed(GameSession session, OpenGump open, GumpCloseReasonType reason)
    {
        try
        {
            open.Gump.OnClosed?.Invoke(session, reason);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Gump {Gump} failed to handle being closed ({Reason})", open.Gump.Id, reason);
        }
    }

    private static GumpState State(GameSession session)
    {
        var state = session.Get(GumpSessionKeys.State);

        if (state is null)
        {
            state = new();
            session.Set(GumpSessionKeys.State, state);
        }

        return state;
    }
}
