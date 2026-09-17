using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Core.Data.Commands;

/// <summary>Immutable metadata describing one registered command and every alias that reaches it.</summary>
public sealed class CommandDefinition
{
    /// <summary>Gets the normalized primary name, used for display and help output.</summary>
    public string Name { get; }

    /// <summary>Gets every normalized alias, including the primary name at index zero.</summary>
    public IReadOnlyList<string> Aliases { get; }

    /// <summary>Gets the help description.</summary>
    public string Description { get; }

    /// <summary>Gets the sources allowed to invoke the command.</summary>
    public CommandSourceType Source { get; }

    /// <summary>Gets the minimum account type required to invoke the command.</summary>
    public AccountType MinimumAccountType { get; }

    /// <summary>Gets the executor type that implements the command.</summary>
    public Type ExecutorType { get; }

    public CommandDefinition(
        string name,
        IReadOnlyList<string> aliases,
        string description,
        CommandSourceType source,
        AccountType minimumAccountType,
        Type executorType
    )
    {
        Name = name;
        Aliases = aliases;
        Description = description;
        Source = source;
        MinimumAccountType = minimumAccountType;
        ExecutorType = executorType;
    }
}
