using System.Text;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Abstractions.Data.Gumps;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Gumps;
using Moongate.Server.Abstractions.Types.Gumps;
using Serilog;

namespace Moongate.Server.Services.Gumps;

/// <summary>
/// Draws gumps and routes their answers back, remembering per session what each open gump drew so a
/// response can be checked against it.
/// </summary>
public sealed class GumpService : IGumpService
{
    private readonly ILogger _logger = Log.ForContext<GumpService>();
    private readonly Dictionary<long, List<OpenGump>> _open = [];
    private readonly Lock _sync = new();

    private uint _nextSerial = 1;

    public void Show(
        PlayerSession session,
        string gumpId,
        Action<IGumpBuilder> build,
        Action<GumpResponse>? onResponse = null
    )
    {
        var builder = new GumpBuilder();

        build(builder);

        var typeId = TypeIdFor(gumpId);
        uint serial;

        lock (_sync)
        {
            serial = _nextSerial++;

            if (!_open.TryGetValue(session.SessionId, out var gumps))
            {
                gumps = [];
                _open[session.SessionId] = gumps;
            }

            gumps.Add(
                new(
                    serial,
                    typeId,
                    gumpId,
                    builder.ButtonIds,
                    builder.SwitchIds,
                    builder.TextEntryIds,
                    onResponse
                )
            );
        }

        session.Send(new CompressedGumpPacket(serial, typeId, 0, 0, builder.Layout, builder.Strings));
    }

    public GumpRejectionType HandleResponse(
        PlayerSession session,
        uint serial,
        int typeId,
        int button,
        IReadOnlyList<int> switches,
        IReadOnlyDictionary<int, string> textEntries
    )
    {
        OpenGump? gump;

        lock (_sync)
        {
            gump = _open.TryGetValue(session.SessionId, out var gumps)
                ? gumps.FirstOrDefault(open => open.Serial == serial && open.TypeId == typeId)
                : null;
        }

        if (gump is null)
        {
            return Reject(session, GumpRejectionType.NotOpen, "unknown");
        }

        var rejection = gump.Validate(button, switches, textEntries);

        if (rejection != GumpRejectionType.None)
        {
            return Reject(session, rejection, gump.GumpId);
        }

        // Forgotten before the callback runs: a gump answers once, and a replay then takes the same
        // path as a fabricated serial.
        lock (_sync)
        {
            if (_open.TryGetValue(session.SessionId, out var gumps))
            {
                gumps.Remove(gump);
            }
        }

        gump.OnResponse?.Invoke(new(button, switches, textEntries));

        return GumpRejectionType.None;
    }

    public bool Close(PlayerSession session, string gumpId)
    {
        lock (_sync)
        {
            if (!_open.TryGetValue(session.SessionId, out var gumps))
            {
                return false;
            }

            var gump = gumps.FirstOrDefault(open => string.Equals(open.GumpId, gumpId, StringComparison.Ordinal));

            return gump is not null && gumps.Remove(gump);
        }
    }

    public int CloseAll(PlayerSession session)
    {
        lock (_sync)
        {
            if (!_open.Remove(session.SessionId, out var gumps))
            {
                return 0;
            }

            return gumps.Count;
        }
    }

    /// <summary>Everything this session has open, for tests and for the admin surface.</summary>
    public IReadOnlyList<OpenGump> OpenFor(PlayerSession session)
    {
        lock (_sync)
        {
            return _open.TryGetValue(session.SessionId, out var gumps) ? [.. gumps] : [];
        }
    }

    /// <summary>
    /// A stable FNV-1a over the id's bytes. The client uses the type to decide whether an incoming
    /// gump replaces one already on screen, so this must not change between restarts —
    /// <c>string.GetHashCode</c> is randomised per process and would silently break that.
    /// </summary>
    private static int TypeIdFor(string gumpId)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        var hash = offset;

        foreach (var b in Encoding.UTF8.GetBytes(gumpId))
        {
            hash = (hash ^ b) * prime;
        }

        return (int)(hash & 0x7FFFFFFF);
    }

    private GumpRejectionType Reject(PlayerSession session, GumpRejectionType rejection, string gumpId)
    {
        _logger.Warning(
            "Refused gump response from session {SessionId} for '{GumpId}': {Rejection}. Disconnecting",
            session.SessionId,
            gumpId,
            rejection
        );

        session.Disconnect();

        return rejection;
    }
}
