using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;

namespace Moongate.Tests.TestSupport.Commands;

public sealed class RecordingCommandExecutor : ICommandExecutor
{
    public List<CommandContext> Invocations { get; } = [];

    public Task ExecuteAsync(CommandContext context)
    {
        Invocations.Add(context);
        context.Print("ok");

        return Task.CompletedTask;
    }
}
