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
[ScriptModule("npc", "Acts as an NPC: speaks, plays sounds, walks and reads where it is.")]
public sealed class NpcModule
{
    public const int MaximumTextLength = 128;

    private const DirectionType DirectionMask = (DirectionType)0x07;

    private readonly IMobileService _mobiles;
    private readonly ISpeechService _speech;
    private readonly IWorldViewService _view;
    private readonly IMobileTemplateService _templates;

    public NpcModule(
        IMobileService mobiles,
        ISpeechService speech,
        IWorldViewService view,
        IMobileTemplateService templates
    )
    {
        _mobiles = mobiles;
        _speech = speech;
        _view = view;
        _templates = templates;
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
    ///     Plays a sound where the NPC stands for the players within 15 cells: a sound id, <c>npc.play_sound(serial, 0x69)</c>,
    ///     or a kind of the NPC template's <c>[mobile.sounds]</c>, <c>npc.play_sound(serial, "idle")</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays a sound id (0 to 65535) or a kind of the NPC's template sounds (start_attack, idle, attack, hurt, death) where the NPC stands; false for an unknown NPC, sound or kind.")]
    public bool PlaySound(long serial, object sound)
    {
        if (!TryGetNpc(serial, out var npc) || ResolveSound(npc, sound) is not { } id)
        {
            return false;
        }

        _speech.PlaySound(npc, id);

        return true;
    }

    /// <summary>
    ///     Turns the NPC toward <paramref name="direction" /> when needed and takes one step, a run when
    ///     <paramref name="running" />; <c>npc.step(serial, DirectionType.North, true)</c>. The players in range see the
    ///     turn and the step. <c>DirectionType.Running</c> is not a direction: pass <paramref name="running" /> instead.
    /// </summary>
    [ScriptFunction(helpText: "One step in a direction, a run when running is true, turning first when needed; false when blocked.")]
    public bool Step(long serial, DirectionType direction, bool running = false)
    {
        if ((direction & ~DirectionMask) != 0 || !TryGetNpc(serial, out var npc))
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
            _view.Moved(npc, oldLocation, running);
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

    // A whole number in the sound range, or a kind the template sets, as UOX3's creature sounds.
    private int? ResolveSound(MobileEntity npc, object sound)
    {
        if (sound is double number)
        {
            return number is >= 0 and <= ushort.MaxValue && Math.Floor(number) == number ? (int)number : null;
        }

        if (sound is not string kind ||
            npc.TemplateId is not { } templateId ||
            !_templates.TryGet(templateId, out var template) ||
            template.Sounds is not { } sounds)
        {
            return null;
        }

        return kind switch
        {
            "start_attack" => sounds.StartAttack,
            "idle" => sounds.Idle,
            "attack" => sounds.Attack,
            "hurt" => sounds.Hurt,
            "death" => sounds.Death,
            _ => null
        };
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
