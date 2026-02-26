using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Modules;

[ScriptModule("speech", "Provides speech sending APIs for scripts.")]

/// <summary>
/// Exposes server-origin speech helpers to Lua scripts.
/// </summary>
public sealed class SpeechModule
{
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public SpeechModule(
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    [ScriptFunction("broadcast", "Broadcasts a server message to all active sessions.")]
    public int Broadcast(string text)
        => string.IsNullOrWhiteSpace(text) ? 0 : _speechService.BroadcastFromServerAsync(text).GetAwaiter().GetResult();

    [ScriptFunction("say", "Sends a server message to a character id.")]
    public bool Say(uint characterId, string text)
    {
        if (characterId == 0 || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId((Serial)characterId, out var session))
        {
            return false;
        }

        return _speechService.SendMessageFromServerAsync(session, text).GetAwaiter().GetResult();
    }

    [ScriptFunction("send", "Sends a server message to a specific session id.")]
    public bool Send(long sessionId, string text)
    {
        if (sessionId <= 0 || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return _gameNetworkSessionService.TryGet(sessionId, out var session) &&
               _speechService.SendMessageFromServerAsync(session, text).GetAwaiter().GetResult();
    }
}
