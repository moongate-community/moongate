using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>npc</c> Lua module: a mobile script acts on its NPC by serial, as <c>npc.say(serial, "Hail")</c>. A serial
///     that is not an NPC in the world gives <c>false</c> or <c>nil</c>, never an error: a script that waited may outlive
///     its NPC, and a script must never move or voice a player.
/// </summary>
[ScriptModule("npc", "Acts as an NPC: speaks, walks and reads where it is.")]
public sealed class NpcModule
{
    public const int MaximumTextLength = 128;

    private readonly IMobileService _mobiles;
    private readonly ISpeechService _speech;
    private readonly IWorldViewService _view;

    public NpcModule(IMobileService mobiles, ISpeechService speech, IWorldViewService view)
    {
        _mobiles = mobiles;
        _speech = speech;
        _view = view;
    }

    /// <summary>
    ///     Makes the NPC say <paramref name="text" /> overhead to the players within 15 cells; <c>npc.say(serial, text)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The NPC says text overhead to the players nearby; false for an unknown NPC or blank text.")]
    public bool Say(long serial, string text)
    {
        if (!TryGetNpc(serial, out var npc) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        _speech.Say(npc, text.Length > MaximumTextLength ? text[..MaximumTextLength] : text);

        return true;
    }

    /// <summary>
    ///     Turns the NPC toward <paramref name="direction" /> when needed and takes one walking step;
    ///     <c>npc.step(serial, DirectionType.North)</c>. The players in range see the turn and the step.
    /// </summary>
    [ScriptFunction(helpText: "One walking step in a direction, turning first when needed; false when blocked.")]
    public bool Step(long serial, DirectionType direction)
    {
        if (!TryGetNpc(serial, out var npc))
        {
            return false;
        }

        var oldLocation = npc.Location;
        var oldDirection = npc.Direction;
        var result = _mobiles.TryMove(npc, direction);

        if (result == MoveResultType.Turned)
        {
            result = _mobiles.TryMove(npc, direction);
        }

        if (npc.Location != oldLocation || npc.Direction != oldDirection)
        {
            _view.Moved(npc, oldLocation, false);
        }

        return result == MoveResultType.Moved;
    }

    /// <summary>
    ///     Gets where the NPC is as <c>{ x, y, z, map }</c>; <c>npc.location(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Where the NPC is, as a table { x, y, z, map }; nil for an unknown NPC.")]
    public LuaTable? Location(long serial)
    {
        if (!TryGetNpc(serial, out var npc))
        {
            return null;
        }

        var table = new LuaTable();
        table["x"] = npc.Location.X;
        table["y"] = npc.Location.Y;
        table["z"] = npc.Location.Z;
        table["map"] = (int)npc.Map;

        return table;
    }

    /// <summary>
    ///     Gets the NPC's name; <c>npc.name(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The NPC's name; nil for an unknown NPC.")]
    public string? Name(long serial)
    {
        return TryGetNpc(serial, out var npc) ? npc.Name : null;
    }

    private bool TryGetNpc(long serial, [NotNullWhen(true)] out MobileEntity? npc)
    {
        npc = null;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out npc) &&
               npc.IsNpc &&
               _mobiles.IsInWorld(npc.Id);
    }
}
