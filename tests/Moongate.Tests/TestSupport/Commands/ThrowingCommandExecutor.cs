using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;

namespace Moongate.Tests.TestSupport.Commands;

public sealed class ThrowingCommandExecutor : ICommandExecutor
{
    public Task ExecuteAsync(CommandContext context)
        => throw new InvalidOperationException("command executor failure");
}
