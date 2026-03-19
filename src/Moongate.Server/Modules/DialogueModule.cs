using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Speech;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("dialogue", "Provides dialogue definition registration helpers for scripts.")]

/// <summary>
/// Exposes dialogue definition registration to Lua scripts.
/// </summary>
public sealed class DialogueModule
{
    private const string DialogueConversationIdKey = "dialogue_id";
    private readonly IDialogueDefinitionService _dialogueDefinitionService;
    private readonly IDialogueRuntimeService _dialogueRuntimeService;
    private readonly ISpeechService _speechService;

    public DialogueModule(
        IDialogueDefinitionService dialogueDefinitionService,
        IDialogueRuntimeService dialogueRuntimeService,
        ISpeechService speechService
    )
    {
        _dialogueDefinitionService = dialogueDefinitionService;
        _dialogueRuntimeService = dialogueRuntimeService;
        _speechService = speechService;
    }

    [ScriptFunction("register", "Registers a Lua-authored conversation definition.")]
    public bool Register(string conversationId, Table? definition)
        => _dialogueDefinitionService.Register(conversationId, definition);

    [ScriptFunction("init", "Binds a registered conversation id to an NPC.")]
    public bool Init(LuaMobileProxy? npc, string conversationId)
    {
        if (npc is null || string.IsNullOrWhiteSpace(conversationId))
        {
            return false;
        }

        npc.Mobile.SetCustomString(DialogueConversationIdKey, conversationId.Trim());

        return true;
    }

    [ScriptFunction("listener", "Handles authored dialogue first for an NPC; returns true when claimed.")]
    public bool Listener(LuaMobileProxy? npc, LuaMobileProxy? speaker, string text)
    {
        if (npc is null || speaker is null || string.IsNullOrWhiteSpace(text) || npc.Serial == speaker.Serial)
        {
            return false;
        }

        if (!npc.Mobile.TryGetCustomString(DialogueConversationIdKey, out var conversationId) ||
            string.IsNullOrWhiteSpace(conversationId))
        {
            return false;
        }

        var normalizedText = text.Trim();
        var session = _dialogueRuntimeService.TryGetActiveSession(npc.Mobile.Id, speaker.Mobile.Id, out _)
                          && TryParseOptionIndex(normalizedText, out var optionIndex)
                              ? _dialogueRuntimeService.ChooseAsync(npc.Mobile, speaker.Mobile, optionIndex)
                                                      .GetAwaiter()
                                                      .GetResult()
                              : _dialogueRuntimeService.HandleTopicAsync(
                                                        npc.Mobile,
                                                        speaker.Mobile,
                                                        conversationId,
                                                        normalizedText
                                                    )
                                                    .GetAwaiter()
                                                    .GetResult();

        if (session is null)
        {
            return false;
        }

        if (!_dialogueDefinitionService.TryGet(session.ConversationId, out var definition) ||
            definition is null ||
            !definition.Nodes.TryGetValue(session.CurrentNodeId, out var node))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(node.Text))
        {
            _ = _speechService.SpeakAsMobileAsync(npc.Mobile, node.Text.Trim()).GetAwaiter().GetResult();
        }

        if (session.VisibleOptions.Count > 0)
        {
            var prompt = string.Join(
                " ",
                session.VisibleOptions.Select((option, index) => $"{index + 1}. {option.Text}")
            );

            _ = _speechService.SpeakAsMobileAsync(npc.Mobile, prompt).GetAwaiter().GetResult();
        }

        return true;
    }

    private static bool TryParseOptionIndex(string text, out int optionIndex)
    {
        optionIndex = 0;

        return int.TryParse(text, out optionIndex) && optionIndex > 0;
    }
}
