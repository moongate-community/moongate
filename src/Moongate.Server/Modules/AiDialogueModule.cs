using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Services.Scripting;

namespace Moongate.Server.Modules;

[ScriptModule("ai_dialogue", "Provides intelligent NPC dialogue helpers backed by OpenAI.")]
public sealed class AiDialogueModule
{
    private readonly INpcAiRuntimeStateService _runtimeStateService;
    private readonly INpcDialogueService _npcDialogueService;

    public AiDialogueModule(
        INpcAiRuntimeStateService runtimeStateService,
        INpcDialogueService npcDialogueService
    )
    {
        _runtimeStateService = runtimeStateService;
        _npcDialogueService = npcDialogueService;
    }

    [ScriptFunction("init", "Binds a prompt file to an NPC for ai_dialogue calls.")]
    public bool Init(LuaMobileProxy? npc, string promptFile)
    {
        if (npc is null || string.IsNullOrWhiteSpace(promptFile))
        {
            return false;
        }

        _runtimeStateService.BindPromptFile(npc.Mobile.Id, promptFile.Trim());

        return true;
    }

    [ScriptFunction("listener", "Handles nearby speech with OpenAI-backed dialogue.")]
    public bool Listener(LuaMobileProxy? npc, LuaMobileProxy? sender, string text)
    {
        if (npc is null || sender is null || string.IsNullOrWhiteSpace(text) || npc.Serial == sender.Serial)
        {
            return false;
        }

        return _npcDialogueService.QueueListener(npc.Mobile, sender.Mobile, text.Trim());
    }

    [ScriptFunction("idle", "Lets an NPC attempt idle chatter when a player is nearby.")]
    public bool Idle(LuaMobileProxy? npc)
    {
        if (npc is null)
        {
            return false;
        }

        return _npcDialogueService.QueueIdle(npc.Mobile);
    }
}
