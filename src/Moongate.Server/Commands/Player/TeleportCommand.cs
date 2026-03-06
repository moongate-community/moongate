using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "teleport|tp",
    "Teleport current player. Usage: .teleport <mapId> <x> <y> <z>",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class TeleportCommand : ICommandExecutor
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public TeleportCommand(
        IGameNetworkSessionService gameNetworkSessionService,
        IGameEventBusService gameEventBusService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _gameEventBusService = gameEventBusService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 4)
        {
            context.Print("Usage: .teleport <mapId> <x> <y> <z>");

            return;
        }

        if (!TryParseArguments(
                context.Arguments,
                out var mapId,
                out var targetLocation
            ))
        {
            context.Print("Usage: .teleport <mapId> <x> <y> <z>");

            return;
        }

        if (!_gameNetworkSessionService.TryGet(context.SessionId, out var session) ||
            session.Character is null ||
            session.CharacterId != session.Character.Id)
        {
            context.PrintError("Cannot teleport: active in-game session not found.");

            return;
        }

        var character = session.Character;
        var oldMapId = character.MapId;
        var oldLocation = character.Location;
        character.MapId = mapId;
        character.Location = targetLocation;

        _outgoingPacketQueue.Enqueue(session.SessionId, new DrawPlayerPacket(character));

        await _gameEventBusService.PublishAsync(
            new MobilePositionChangedEvent(
                session.SessionId,
                character.Id,
                oldMapId,
                mapId,
                oldLocation,
                targetLocation
            )
        );

        context.Print(
            "Teleported to map {0} at ({1}, {2}, {3}).",
            mapId,
            targetLocation.X,
            targetLocation.Y,
            targetLocation.Z
        );
    }

    private static bool TryParseArguments(string[] arguments, out int mapId, out Point3D location)
    {
        location = Point3D.Zero;

        if (!int.TryParse(arguments[0], out mapId) ||
            !int.TryParse(arguments[1], out var x) ||
            !int.TryParse(arguments[2], out var y) ||
            !int.TryParse(arguments[3], out var z))
        {
            return false;
        }

        if (mapId < 0)
        {
            return false;
        }

        location = new(x, y, z);

        return true;
    }
}
