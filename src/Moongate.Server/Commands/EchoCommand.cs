using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;

namespace Moongate.Server.Commands;

/// <summary>Writes the command arguments back to the caller.</summary>
public sealed class EchoCommand : ICommandExecutor
{
    /// <inheritdoc />
    public Task ExecuteAsync(CommandContext context)
    {
        context.Print(string.Join(' ', context.Arguments));

        return Task.CompletedTask;
    }
}
