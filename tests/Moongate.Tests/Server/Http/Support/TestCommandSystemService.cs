using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http.Support;

public sealed class TestCommandSystemService : ICommandSystemService
{
    public Func<string, CommandSourceType, GameSession?, CancellationToken, Task> ExecuteCommandAsyncImpl { get; init; } =
        (_, _, _, _) => Task.CompletedTask;

    public Func<string, CommandSourceType, GameSession?, CancellationToken, Task<IReadOnlyList<string>>>
        ExecuteCommandWithOutputAsyncImpl { get; init; } = (_, _, _, _) => Task.FromResult<IReadOnlyList<string>>([]);

    public int RegisterCommandCalls { get; private set; }

    public Task ExecuteCommandAsync(
        string commandWithArgs,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
        => ExecuteCommandAsyncImpl(commandWithArgs, source, session, cancellationToken);

    public Task<IReadOnlyList<string>> ExecuteCommandWithOutputAsync(
        string commandWithArgs,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
        => ExecuteCommandWithOutputAsyncImpl(commandWithArgs, source, session, cancellationToken);

    public IReadOnlyList<string> GetAutocompleteSuggestions(string commandWithArgs)
        => [];

    public void RegisterCommand(
        string commandName,
        Func<CommandSystemContext, Task> handler,
        string description = "",
        CommandSourceType source = CommandSourceType.Console,
        AccountType minimumAccountType = AccountType.Administrator,
        Func<CommandAutocompleteContext, IReadOnlyList<string>>? autocompleteProvider = null
    )
        => RegisterCommandCalls++;

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
