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
    }

    private static async Task OnSaveCommand(CommandSystemContext context)
    {
        var persistenceService = MoongateContext.Container.Resolve<IPersistenceService>();

        persistenceService.RequestSave();
    }
}
