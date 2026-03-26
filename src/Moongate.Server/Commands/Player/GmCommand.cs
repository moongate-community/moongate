using Moongate.Scripting.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "gm",
    "Open the GM menu. Usage: .gm",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class GmCommand : ICommandExecutor
{
    private readonly IScriptEngineService _scriptEngineService;

    public GmCommand(IScriptEngineService scriptEngineService)
    {
        _scriptEngineService = scriptEngineService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.Print("Usage: .gm");

            return Task.CompletedTask;
        }

        if (!context.CharacterId.IsValid)
        {
            return Task.CompletedTask;
        }

        _scriptEngineService.CallFunction("on_gm_menu_request", context.SessionId, (uint)context.CharacterId);

        return Task.CompletedTask;
    }
}
