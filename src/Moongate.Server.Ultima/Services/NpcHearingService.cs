using Lua;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Tells the NPCs within the range of the speech (a whisper carries 1 cell, a yell 18) what a player said:
///     <c>on_speech(serial, speaker, text, keywords, type)</c> of their mobile script, the keywords an array of
///     speech.mul ids and the type a <c>SpeechType</c>. The handler may wait: it runs once per speech.
/// </summary>
public sealed class NpcHearingService : INpcSpeechListener
{
    private readonly INpcScriptService _scripts;
    private readonly ISectorService _sectors;

    public NpcHearingService(INpcScriptService scripts, ISectorService sectors)
    {
        _scripts = scripts;
        _sectors = sectors;
    }

    public void Heard(
        MobileEntity speaker,
        string text,
        IReadOnlyList<int>? keywords = null,
        SpeechType type = SpeechType.Regular
    )
    {
        foreach (var npc in _sectors.GetMobilesInRange(speaker.Map, speaker.Location, type.Range))
        {
            if (npc.IsNpc)
            {
                _scripts.Run(npc, "on_speech", (long)speaker.Id.Value, text, Table(keywords), (long)type);
            }
        }
    }

    // One table per NPC: a script that changes its table cannot change what the next NPC hears.
    private static LuaTable Table(IReadOnlyList<int>? keywords)
    {
        var table = new LuaTable();

        for (var index = 0; index < keywords?.Count; index++)
        {
            table[index + 1] = keywords[index];
        }

        return table;
    }
}
