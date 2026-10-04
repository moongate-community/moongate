using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;
using Serilog;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Puts an item made from an item template on the ground where the game master targets: <c>add &lt;template&gt;</c>.
///     A container comes with the gold and the loot of its template, as one a spawn region makes.
/// </summary>
public sealed class AddCommand : ICommandExecutor
{
    private readonly ILogger _logger = Log.ForContext<AddCommand>();
    private readonly IItemSpawnService _spawns;
    private readonly IItemTemplateService _templates;
    private readonly ITargetService _targets;
    private readonly ILocalizationService? _localization;

    public AddCommand(
        IItemSpawnService spawns,
        IItemTemplateService templates,
        ITargetService targets,
        ILocalizationService? localization = null
    )
    {
        _localization = localization;
        _spawns = spawns;
        _templates = templates;
        _targets = targets;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("add works in game only.");

            return;
        }

        if (context.Arguments.Length != 1)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "add <template>"));

            return;
        }

        var templateId = context.Arguments[0];

        if (!_templates.TryGet(templateId, out _))
        {
            context.PrintError(_localization.Text(CommandMessages.UnknownItemTemplate, "Unknown item template: {0}", templateId));

            return;
        }

        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Location,
            TargetFlagsType.Neutral,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Location)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        try
        {
            var item = await _spawns.SpawnAsync(templateId, target.Map, target.Location, cancellationToken: context.CancellationToken);
            var spot = item.GroundLocation ?? target.Location;
            context.Print(
                _localization.Text(
                    CommandMessages.ItemAdded,
                    "Added {0} ({1}) at {2} ({3}, {4}, {5}).",
                    item.Name ?? item.TemplateId,
                    item.Id,
                    target.Map,
                    spot.X,
                    spot.Y,
                    spot.Z
                )
            );
        }
        catch (Exception exception)
        {
            // The exception is English and technical: the GM gets the reason from the log.
            _logger.Error(exception, "Adding {Template} failed", templateId);
            context.PrintError(_localization.Text(CommandMessages.AddFailed, "The item could not be added. Check the server logs."));
        }
    }
}
