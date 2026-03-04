using Moongate.Network.Packets.Incoming.UI;
using Moongate.Server.Data.Session;
using MoonSharp.Interpreter;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Dispatches incoming gump button responses to Lua callbacks.
/// </summary>
public interface IGumpScriptDispatcherService
{
    /// <summary>
    /// Registers a Lua callback for a specific gump and button identifier.
    /// </summary>
    void RegisterHandler(uint gumpId, uint buttonId, Closure handler);

    /// <summary>
    /// Tries to dispatch an incoming gump response to a registered handler.
    /// </summary>
    bool TryDispatch(GameSession session, GumpMenuSelectionPacket packet);
}

