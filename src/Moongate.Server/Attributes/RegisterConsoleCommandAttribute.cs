using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Attributes;

/// <summary>
/// Marks a command executor for source-generated console command registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RegisterConsoleCommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new attribute for command registration.
    /// </summary>
    /// <param name="commandName">Command name or aliases separated by <c>|</c>.</param>
    /// <param name="description">Command description.</param>
    /// <param name="source">Allowed command source flags.</param>
    /// <param name="minimumAccountType">Minimum required account type.</param>
    public RegisterConsoleCommandAttribute(
        string commandName,
        string description = "",
        CommandSourceType source = CommandSourceType.Console,
        AccountType minimumAccountType = AccountType.Administrator
    )
    {
        CommandName = commandName;
        Description = description;
        Source = source;
        MinimumAccountType = minimumAccountType;
    }

    /// <summary>
    /// Gets the command name or aliases.
    /// </summary>
    public string CommandName { get; }

    /// <summary>
    /// Gets the command description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets allowed command source flags.
    /// </summary>
    public CommandSourceType Source { get; }

    /// <summary>
    /// Gets minimum account type required to execute the command.
    /// </summary>
    public AccountType MinimumAccountType { get; }
}
