using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Internal.Entities;

/// <summary>
/// Lua-safe mobile reference wrapper.
/// </summary>
public sealed class LuaMobileRef
{
    private readonly UOMobileEntity _mobile;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public uint Serial => (uint)_mobile.Id;

    public string Name => _mobile.Name ?? string.Empty;

    public int MapId => _mobile.MapId;

    public int LocationX => _mobile.Location.X;

    public int LocationY => _mobile.Location.Y;

    public int LocationZ => _mobile.Location.Z;

    public bool IsOnline => _gameNetworkSessionService.TryGetByCharacterId(_mobile.Id, out _);

    public LuaMobileRef(
        UOMobileEntity mobile,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _mobile = mobile;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public bool Say(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(_mobile.Id, out GameSession session))
        {
            return false;
        }

        return _speechService.SendMessageFromServerAsync(session, text).GetAwaiter().GetResult();
    }
}
