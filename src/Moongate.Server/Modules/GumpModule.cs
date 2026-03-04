using Moongate.Scripting.Attributes.Scripts;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Server.Modules.Builders;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("gump", "Provides fluent gump layout building APIs.")]

/// <summary>
/// Exposes gump-building helpers to Lua scripts.
/// </summary>
public sealed class GumpModule
{
    private static bool _isBuilderTypeRegistered;
    private readonly IOutgoingPacketQueue? _outgoingPacketQueue;
    private readonly IGameNetworkSessionService? _gameNetworkSessionService;
    private readonly IGumpScriptDispatcherService? _gumpScriptDispatcherService;

    public GumpModule(
        IOutgoingPacketQueue? outgoingPacketQueue = null,
        IGameNetworkSessionService? gameNetworkSessionService = null,
        IGumpScriptDispatcherService? gumpScriptDispatcherService = null
    )
    {
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
        _gumpScriptDispatcherService = gumpScriptDispatcherService;
    }

    [ScriptFunction("create", "Creates a new gump builder instance.")]
    public LuaGumpBuilder Create()
    {
        if (!_isBuilderTypeRegistered)
        {
            UserData.RegisterType<LuaGumpBuilder>();
            _isBuilderTypeRegistered = true;
        }

        return new();
    }

    [ScriptFunction("send", "Sends a compressed gump to a target session.")]
    public bool Send(
        long sessionId,
        LuaGumpBuilder builder,
        uint senderSerial = 0,
        uint gumpId = 1,
        uint x = 50,
        uint y = 50
    )
    {
        if (sessionId <= 0 || builder is null || _outgoingPacketQueue is null || _gameNetworkSessionService is null)
        {
            return false;
        }

        if (!_gameNetworkSessionService.TryGet(sessionId, out _))
        {
            return false;
        }

        var packet = new CompressedGumpPacket
        {
            SenderSerial = senderSerial,
            GumpId = gumpId,
            X = x,
            Y = y,
            Layout = builder.BuildLayout()
        };

        packet.TextLines.AddRange(builder.BuildTexts());
        _outgoingPacketQueue.Enqueue(sessionId, packet);

        return true;
    }

    [ScriptFunction("on", "Registers a Lua callback for a gump button response.")]
    public bool On(uint gumpId, uint buttonId, Closure handler)
    {
        if (gumpId == 0 || buttonId == 0 || handler is null || _gumpScriptDispatcherService is null)
        {
            return false;
        }

        _gumpScriptDispatcherService.RegisterHandler(gumpId, buttonId, handler);

        return true;
    }
}
