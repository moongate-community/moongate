using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;

namespace Moongate.Core.Server.Data.Internal.Commands;

public class CommandDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public AccountLevelType AccountLevel { get; set; }
    public CommandSourceType Source { get; set; }

    public ICommandSystemService.CommandHandlerDelegate Handler { get; set; }
}
