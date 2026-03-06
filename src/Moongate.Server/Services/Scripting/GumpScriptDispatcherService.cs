using Moongate.Network.Packets.Incoming.UI;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Ids;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Default Lua gump response dispatcher.
/// </summary>
public sealed class GumpScriptDispatcherService : IGumpScriptDispatcherService
{
    private readonly Dictionary<(uint GumpId, uint ButtonId), Closure> _handlers = [];
    private readonly Lock _syncRoot = new();
    private readonly ILogger _logger = Log.ForContext<GumpScriptDispatcherService>();

    public void RegisterHandler(uint gumpId, uint buttonId, Closure handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_syncRoot)
        {
            _handlers[(gumpId, buttonId)] = handler;
        }
    }

    public bool TryDispatch(GameSession session, GumpMenuSelectionPacket packet)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(packet);

        Closure? callback;

        lock (_syncRoot)
        {
            _handlers.TryGetValue((packet.GumpId, packet.ButtonId), out callback);
        }

        if (callback is null)
        {
            return false;
        }

        try
        {
            callback.OwnerScript.Call(
                callback,
                new Dictionary<string, object?>
                {
                    ["session_id"] = session.SessionId,
                    ["character_id"] = session.CharacterId == Serial.Zero
                                           ? null
                                           : (uint)session.CharacterId,
                    ["gump_id"] = packet.GumpId,
                    ["button_id"] = packet.ButtonId,
                    ["serial"] = packet.Serial,
                    ["switches"] = packet.Switches.ToArray(),
                    ["text_entries"] = packet.TextEntries.ToDictionary(
                        static pair => pair.Key,
                        static pair => pair.Value
                    )
                }
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to dispatch gump callback GumpId={GumpId} ButtonId={ButtonId}",
                packet.GumpId,
                packet.ButtonId
            );

            return false;
        }
    }
}
