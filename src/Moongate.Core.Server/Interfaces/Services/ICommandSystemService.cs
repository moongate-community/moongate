using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.Core.Server.Types;

namespace Moongate.Core.Server.Interfaces.Services;

public interface ICommandSystemService : IMoongateService
{
    delegate Task CommandHandlerDelegate(CommandSystemContext context);


    void RegisterCommand(string commandName,
        CommandHandlerDelegate handler,
        string description = "",
        AccountLevelType accountLevel = AccountLevelType.User,
        CommandSourceType source = CommandSourceType.All);
}
