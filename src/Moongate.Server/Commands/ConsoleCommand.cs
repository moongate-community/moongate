using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Commands;

/// <summary>
///     Locks the console input again, as it is at startup, so stray keys reach no command: <c>console lock</c>. The
///     unlock key opens it.
/// </summary>
public sealed class ConsoleCommand : ICommandExecutor, ICommandArgumentCompleter
{
    private const string UsageText = "console lock";

    private readonly IConsolePromptService _prompt;
    private readonly ILocalizationService? _localization;

    public ConsoleCommand(IConsolePromptService prompt, ILocalizationService? localization = null)
    {
        _prompt = prompt;
        _localization = localization;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments is not ["lock"])
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return Task.CompletedTask;
        }

        _prompt.LockInput();
        context.Print(
            _localization.Text(CommandMessages.ConsoleLocked, "Console locked. Press '{0}' to unlock.", _prompt.UnlockCharacter)
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetArgumentCompletions(IReadOnlyList<string> previousArguments)
    {
        return previousArguments.Count == 0 ? ["lock"] : [];
    }

}
