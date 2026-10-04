using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;

namespace Moongate.Tests.TestSupport.Commands;

/// <summary>
///     A command that completes its first argument with <see cref="Values" /> and records what it was asked; throws when
///     <see cref="Failure" /> is set.
/// </summary>
public sealed class CompletingCommandExecutor : ICommandExecutor, ICommandArgumentCompleter
{
    public List<string> Values { get; } = ["first", "second"];

    public List<IReadOnlyList<string>> Asked { get; } = [];

    public Exception? Failure { get; set; }

    public Task ExecuteAsync(CommandContext context)
    {
        context.Print("ok");

        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetArgumentCompletions(IReadOnlyList<string> previousArguments)
    {
        Asked.Add(previousArguments);

        return Failure is null ? Values : throw Failure;
    }
}
