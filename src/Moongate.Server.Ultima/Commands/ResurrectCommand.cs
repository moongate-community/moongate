using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Death;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Raises the NPC whose corpse the game master targets: one of the same template is born where the corpse lies, with
///     the name of who died, and the corpse is gone.
/// </summary>
public sealed class ResurrectCommand : ICommandExecutor
{
    private readonly IDeathService _death;
    private readonly ITargetService _targets;
    private readonly ILocalizationService? _localization;

    public ResurrectCommand(IDeathService death, ITargetService targets, ILocalizationService? localization = null)
    {
        _death = death;
        _targets = targets;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("resurrect works in game only.");

            return;
        }

        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Beneficial,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        var result = await _death.ResurrectAsync(target.Serial, context.CancellationToken);
        context.Print(
            result.Type switch
            {
                ResurrectResultType.Raised => _localization.Text(
                    CommandMessages.Resurrected,
                    "{0} is back.",
                    result.Mobile?.Name ?? ""
                ),
                ResurrectResultType.CannotBeRaised => _localization.Text(
                    CommandMessages.CorpseCannotBeRaised,
                    "That corpse cannot be raised."
                ),
                _ => _localization.Text(CommandMessages.NotACorpse, "That is not a corpse.")
            }
        );
    }
}
