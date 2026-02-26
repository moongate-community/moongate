using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Ids;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("mobile", "Provides helpers to resolve mobiles from scripts.")]

/// <summary>
/// Exposes mobile lookup helpers to Lua scripts.
/// </summary>
public sealed class MobileModule
{
    private static bool _isLuaMobileRefTypeRegistered;
    private readonly ICharacterService _characterService;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public MobileModule(
        ICharacterService characterService,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _characterService = characterService;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    [ScriptFunction("get", "Gets a mobile reference by character id, or nil when not found.")]
    public LuaMobileRef? Get(uint characterId)
    {
        if (characterId == 0)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var mobile = _characterService.GetCharacterAsync((Serial)characterId).GetAwaiter().GetResult();

        return mobile is null ? null : new(mobile, _speechService, _gameNetworkSessionService);
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaMobileRefTypeRegistered)
        {
            return;
        }

        UserData.RegisterType<LuaMobileRef>();
        _isLuaMobileRefTypeRegistered = true;
    }
}
