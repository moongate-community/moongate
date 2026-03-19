using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Opens the bank container for the current player.
/// </summary>
[RegisterConsoleCommand("bank", "Open your bank box.", CommandSourceType.InGame, AccountType.Regular)]
public sealed class BankCommand : ICommandExecutor
{
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public BankCommand(
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _gameSessionService = gameSessionService;
        _characterService = characterService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        var error = await BankBoxOpenHelper.OpenAsync(
            context.SessionId,
            _gameSessionService,
            _characterService,
            _outgoingPacketQueue
        );

        if (!string.IsNullOrWhiteSpace(error))
        {
            context.Print(error);
            return;
        }
        context.Print("Bank box opened.");
    }
}
