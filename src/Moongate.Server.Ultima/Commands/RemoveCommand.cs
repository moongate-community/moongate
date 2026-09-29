using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Removes the NPC the game master targets; players and items are refused.
/// </summary>
public sealed class RemoveCommand : ICommandExecutor
{
    private readonly INpcService _npcs;
    private readonly ITargetService _targets;
    private readonly ILocalizationService? _localization;

    public RemoveCommand(INpcService npcs, ITargetService targets, ILocalizationService? localization = null)
    {
        _localization = localization;
        _npcs = npcs;
        _targets = targets;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("remove works in game only.");

            return;
        }

        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Harmful,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        if (await _npcs.RemoveAsync(target.Serial, context.CancellationToken))
        {
            context.Print(_localization.Text(CommandMessages.Removed, "Removed {0}.", target.Serial));
        }
        else
        {
            context.Print(_localization.Text(CommandMessages.NotAnNpc, "That is not an NPC."));
        }
    }
}
