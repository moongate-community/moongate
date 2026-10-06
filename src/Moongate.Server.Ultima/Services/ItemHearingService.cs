using Lua;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Lets the scripted items on the ground hear the players around them: each one within
///     <see cref="HearingRange" /> cells runs
///     <c>on_speech(serial, speaker, text, keywords)</c>, as ModernUO's items that
///     handle speech, such as the teleporter that answers a word.
/// </summary>
public sealed class ItemHearingService : IItemSpeechListener
{
    public const int HearingRange = SpeechService.SayRange;

    private const string SpeechFunction = "on_speech";

    private readonly ISectorService _sectors;
    private readonly IItemScriptService _scripts;

    public ItemHearingService(ISectorService sectors, IItemScriptService scripts)
    {
        _sectors = sectors;
        _scripts = scripts;
    }

    public void Heard(MobileEntity speaker, string text, IReadOnlyList<int>? keywords = null)
    {
        // The list is the service's own copy: a script may move or delete items.
        foreach (var item in _sectors.GetItemsInRange(speaker.Map, speaker.Location, HearingRange))
        {
            if (_scripts.HasScript(item))
            {
                _scripts.Run(item, SpeechFunction, (long)speaker.Id.Value, text, Table(keywords));
            }
        }
    }

    // One table per item: a script that changes its table cannot change what the next item hears.
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
