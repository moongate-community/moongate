using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Tests.TestSupport.Commands;

/// <summary>
///     Records the command lines it is asked to run, with their source and session, and answers the lines a test
///     gives it.
/// </summary>
public sealed class RecordingCommandSystemService : ICommandSystemService
{
    public List<(string Line, CommandSourceType Source, GameSession? Session)> Executed { get; } = [];

    /// <summary>
    ///     What every command answers.
    /// </summary>
    public List<CommandOutputLine> Output { get; } = [];

    /// <summary>
    ///     What every command throws after it is recorded; null for none.
    /// </summary>
    public Exception? Throws { get; set; }

    public Task<IReadOnlyList<CommandOutputLine>> ExecuteAsync(
        string commandLine,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
    {
        Executed.Add((commandLine, source, session));

        return Throws is null
            ? Task.FromResult<IReadOnlyList<CommandOutputLine>>(Output.ToArray())
            : Task.FromException<IReadOnlyList<CommandOutputLine>>(Throws);
    }

    public IReadOnlyList<CommandDefinition> GetRegisteredCommands()
    {
        return [];
    }

    public IReadOnlyList<string> GetArgumentCompletions(
        string commandName,
        IReadOnlyList<string> previousArguments,
        CommandSourceType source = CommandSourceType.Console
    )
    {
        return [];
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
