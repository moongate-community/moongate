using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Tells the NPCs within the say range what a player said: <c>on_speech(serial, speaker, text)</c> of their mobile
///     script. The handler may wait: it runs once per speech.
/// </summary>
public sealed class NpcHearingService : INpcSpeechListener
{
    public const int HearingRange = SpeechService.SayRange;

    private readonly NpcScriptService _scripts;
    private readonly ISectorService _sectors;

    public NpcHearingService(NpcScriptService scripts, ISectorService sectors)
    {
        _scripts = scripts;
        _sectors = sectors;
    }

    public void Heard(MobileEntity speaker, string text)
    {
        foreach (var npc in _sectors.GetMobilesInRange(speaker.Map, speaker.Location, HearingRange))
        {
            if (npc.IsNpc)
            {
                _scripts.Run(npc, "on_speech", (long)speaker.Id.Value, text);
            }
        }
    }
}
