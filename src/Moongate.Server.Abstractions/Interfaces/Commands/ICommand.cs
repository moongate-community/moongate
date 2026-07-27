using Moongate.Server.Abstractions.Data.Commands;

namespace Moongate.Server.Abstractions.Interfaces.Commands;

/// <summary>A GM/admin command reachable by typing its name (with any alias) after the "." prefix.</summary>
public interface ICommand
{
    void Execute(CommandContext context);
}
