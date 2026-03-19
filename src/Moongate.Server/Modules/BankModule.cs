using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;

namespace Moongate.Server.Modules;

[ScriptModule("bank", "Provides classic bank box open helpers.")]
public sealed class BankModule
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ICharacterService _characterService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public BankModule(
        IGameNetworkSessionService gameNetworkSessionService,
        ICharacterService characterService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _characterService = characterService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    [ScriptFunction("open", "Opens the bank box for the specified player session.")]
    public bool Open(long sessionId)
        => BankBoxOpenHelper.OpenAsync(
               sessionId,
               _gameNetworkSessionService,
               _characterService,
               _outgoingPacketQueue
           )
           .GetAwaiter()
           .GetResult() is null;
}
