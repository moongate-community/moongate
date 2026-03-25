using Moongate.Network.Packets.Types.Targeting;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("target", "Provides target cursor request helpers for Lua scripts.")]
public sealed class TargetModule
{
    private readonly IPlayerTargetService _playerTargetService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public TargetModule(
        IPlayerTargetService playerTargetService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _playerTargetService = playerTargetService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    [ScriptFunction("cancel", "Cancels a pending target cursor by cursor id.")]
    public bool Cancel(long sessionId, uint cursorId)
    {
        if (sessionId <= 0 || cursorId == 0)
        {
            return false;
        }

        _playerTargetService.SendCancelTargetCursorAsync(sessionId, (Serial)cursorId).GetAwaiter().GetResult();

        return true;
    }

    [ScriptFunction("request_location", "Requests a location target cursor and dispatches the result to a Lua closure.")]
    public uint RequestLocation(long sessionId, Closure handler, int cursorType = (int)TargetCursorType.Neutral)
    {
        if (sessionId <= 0 || handler is null)
        {
            return 0;
        }

        var requestedCursorType = (TargetCursorType)cursorType;
        var cursorId = _playerTargetService.SendTargetCursorAsync(
                                               sessionId,
                                               callback => InvokeHandler(handler, sessionId, callback),
                                               TargetCursorSelectionType.SelectLocation,
                                               requestedCursorType
                                           )
                                           .GetAwaiter()
                                           .GetResult();

        return (uint)cursorId;
    }

    private void InvokeHandler(Closure handler, long sessionId, Data.Internal.Cursors.PendingCursorCallback callback)
    {
        var script = handler.OwnerScript;

        if (script is null)
        {
            return;
        }

        var payload = new Table(script)
        {
            ["x"] = callback.Packet.Location.X,
            ["y"] = callback.Packet.Location.Y,
            ["z"] = callback.Packet.Location.Z,
            ["cursor_id"] = (uint)callback.Packet.CursorId
        };

        if (_gameNetworkSessionService.TryGet(sessionId, out var session) && session.Character is not null)
        {
            payload["map_id"] = session.Character.MapId;
            payload["character_id"] = (uint)session.Character.Id;
        }
        else
        {
            payload["map_id"] = 0;
        }

        script.Call(handler, payload);
    }
}
