using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;

namespace Moongate.Server.Modules;


[ScriptModule("commands")]
public class CommandsModule
{
    private readonly ICommandSystemService _commandSystemService;

    public CommandsModule(ICommandSystemService commandSystemService)
    {
        _commandSystemService = commandSystemService;
    }

    [ScriptFunction("Register new command")]
    public void RegisterCommand(string commandName, Func<CommandSystemContext,Task> handler, string description = "",
        AccountLevelType accountLevel = AccountLevelType.User, CommandSourceType source = CommandSourceType.All)
    {
        _commandSystemService.RegisterCommand(commandName, (ctx) => handler(ctx), description, accountLevel, source);
    }
}
