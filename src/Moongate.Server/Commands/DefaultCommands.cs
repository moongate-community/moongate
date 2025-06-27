using DryIoc;
using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Interfaces.Services;

namespace Moongate.Server.Commands;

public static class DefaultCommands
{
    public static void RegisterDefaultCommands(ICommandSystemService commandSystemService)
    {
        commandSystemService.RegisterCommand(
            "save",
            OnSaveCommand,
            "Requests a save of the current game state.",
            AccountLevelType.Admin,
            CommandSourceType.Console
        );

        commandSystemService.RegisterCommand(
            "where",
            OnWhereCommand,
            "Returns the current location",
            AccountLevelType.User,
            CommandSourceType.InGame
        );
    }

    private static async Task OnWhereCommand(CommandSystemContext context)
    {
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();

        var mobile = gameSessionService.GetSession(context.SessionId).Mobile;
        context.Print(
            "You are at Map {0} (X: {1}, Y: {2}, Z: {3})",
            mobile.Map.MapID,
            mobile.Location.X,
            mobile.Location.Y,
            mobile.Location.Z
        );
    }

    private static async Task OnSaveCommand(CommandSystemContext context)
    {
        var persistenceService = MoongateContext.Container.Resolve<IPersistenceService>();

        persistenceService.RequestSave();
    }

}
