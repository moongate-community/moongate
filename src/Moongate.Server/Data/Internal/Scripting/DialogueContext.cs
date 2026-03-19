using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Scripting;

[RegisterLuaUserData]
public sealed class DialogueContext
{
    private readonly UOMobileEntity _speakerMobile;
    private readonly UOMobileEntity _listenerMobile;
    private readonly DialogueSession _session;
    private readonly DialogueMemoryEntry _memory;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private LuaMobileRef? _speaker;
    private LuaMobileRef? _listener;

    public DialogueContext(
        UOMobileEntity speakerMobile,
        UOMobileEntity listenerMobile,
        DialogueSession session,
        DialogueMemoryEntry memory,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _speakerMobile = speakerMobile;
        _listenerMobile = listenerMobile;
        _session = session;
        _memory = memory;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public LuaMobileRef speaker
        => _speaker ??= new(_speakerMobile, _speechService, _gameNetworkSessionService);

    public LuaMobileRef listener
        => _listener ??= new(_listenerMobile, _speechService, _gameNetworkSessionService);

    public string conversation_id => _session.ConversationId;

    public string node_id => _session.CurrentNodeId;

    public bool EndRequested { get; private set; }

    public bool get_flag(string key)
        => !string.IsNullOrWhiteSpace(key) &&
           _session.SessionFlags.TryGetValue(key.Trim(), out var value) &&
           value;

    public void set_flag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _session.SessionFlags[key.Trim()] = value;
    }

    public bool get_memory_flag(string key)
        => !string.IsNullOrWhiteSpace(key) &&
           _memory.Flags.TryGetValue(key.Trim(), out var value) &&
           value;

    public void set_memory_flag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _memory.Flags[key.Trim()] = value;
    }

    public long get_memory_number(string key)
        => !string.IsNullOrWhiteSpace(key) &&
           _memory.Numbers.TryGetValue(key.Trim(), out var value)
               ? value
               : 0;

    public void set_memory_number(string key, long value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _memory.Numbers[key.Trim()] = value;
    }

    public long add_memory_number(string key, long delta)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        var normalizedKey = key.Trim();
        var nextValue = get_memory_number(normalizedKey) + delta;
        _memory.Numbers[normalizedKey] = nextValue;

        return nextValue;
    }

    public string get_memory_text(string key)
        => !string.IsNullOrWhiteSpace(key) &&
           _memory.Texts.TryGetValue(key.Trim(), out var value)
               ? value
               : string.Empty;

    public void set_memory_text(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _memory.Texts[key.Trim()] = value?.Trim() ?? string.Empty;
    }

    public bool say(string text)
        => Speak(text, ChatMessageType.Regular);

    public bool emote(string text)
        => Speak(text, ChatMessageType.Emote);

    public bool yell(string text)
        => Speak(text, ChatMessageType.Yell);

    public bool whisper(string text)
        => Speak(text, ChatMessageType.Whisper);

    public bool end_conversation()
    {
        EndRequested = true;

        return true;
    }

    private bool Speak(string text, ChatMessageType messageType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var recipients = _speechService.SpeakAsMobileAsync(_speakerMobile, text.Trim(), messageType: messageType)
                                       .GetAwaiter()
                                       .GetResult();

        return recipients > 0;
    }
}
