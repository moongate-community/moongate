using Moongate.Scripting.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Tells the NPCs within the say range what a player said: <c>on_speech(serial, speaker, text)</c> of the table
///     their template names with <c>script_id</c>. The handler may wait: it runs once per speech.
/// </summary>
public sealed class NpcHearingService : INpcSpeechListener
{
    public const int HearingRange = SpeechService.SayRange;

    private readonly IScriptEngine _engine;
    private readonly IMobileTemplateService _templates;
    private readonly ISectorService _sectors;

    public NpcHearingService(IScriptEngine engine, IMobileTemplateService templates, ISectorService sectors)
    {
        _engine = engine;
        _templates = templates;
        _sectors = sectors;
    }

    public void Heard(MobileEntity speaker, string text)
    {
        foreach (var npc in _sectors.GetMobilesInRange(speaker.Map, speaker.Location, HearingRange))
        {
            if (npc.IsNpc &&
                npc.TemplateId is { } id &&
                _templates.TryGet(id, out var template) &&
                template.ScriptId is { } script)
            {
                _engine.CallMember(script, "on_speech", (long)npc.Id.Value, (long)speaker.Id.Value, text);
            }
        }
    }
}
