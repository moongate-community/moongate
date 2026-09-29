using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Commands;

/// <summary>
///     Lists commands available to the caller and describes a command by name or alias.
/// </summary>
public sealed class HelpCommand : ICommandExecutor
{
    private readonly CommandRegistry _registry;
    private readonly ILocalizationService? _localization;

    public HelpCommand(CommandRegistry registry, ILocalizationService? localization = null)
    {
        _registry = registry;
        _localization = localization;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length > 1)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "help [command]"));

            return Task.CompletedTask;
        }

        var available = _registry.Registrations
            .Values
            .Select(registration => registration.Definition)
            .Distinct()
            .Where(definition => IsAvailable(definition, context))
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (context.Arguments.Length == 0)
        {
            context.Print(_localization.Text(CommandMessages.AvailableCommands, "Available commands:"));

            foreach (var definition in available)
            {
                context.Print($"{definition.Name} - {Description(definition)}");
            }

            return Task.CompletedTask;
        }

        var name = context.Arguments[0];
        var command =
            available.FirstOrDefault(definition => definition.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase)
            );

        if (command is null)
        {
            context.PrintError(_localization.Text(CommandMessages.UnknownOrUnavailableCommand, "Unknown or unavailable command: {0}", name));

            return Task.CompletedTask;
        }

        // Sources and account levels are technical names, as the commands take them.
        context.Print(_localization.Text(CommandMessages.HelpCommand, "Command: {0}", command.Name));
        context.Print(_localization.Text(CommandMessages.HelpDescription, "Description: {0}", Description(command)));
        context.Print(_localization.Text(CommandMessages.HelpAliases, "Aliases: {0}", string.Join(", ", command.Aliases)));
        context.Print(_localization.Text(CommandMessages.HelpSources, "Sources: {0}", command.Source));
        context.Print(_localization.Text(CommandMessages.HelpMinimumAccountLevel, "Minimum account level: {0}", command.MinimumAccountType));

        return Task.CompletedTask;
    }

    private string Description(CommandDefinition definition)
    {
        // The registered description is shown as is: it is not a format, and may hold braces.
        return definition.DescriptionMessage != 0 &&
               _localization is not null &&
               _localization.TryGetText(definition.DescriptionMessage, out var text)
                   ? text
                   : definition.Description;
    }

    private static bool IsAvailable(CommandDefinition definition, CommandContext context)
    {
        if (context.Source == CommandSourceType.None || !definition.Source.HasFlag(context.Source))
        {
            return false;
        }

        var level = context.Source switch
        {
            CommandSourceType.Console => AccountType.Administrator,
            CommandSourceType.InGame  => context.Session?.AccountType ?? AccountType.Regular,
            _                         => AccountType.Regular
        };

        return level >= definition.MinimumAccountType;
    }
}
