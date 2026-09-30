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
///     Spawns an NPC from a mobile template where the game master targets: <c>spawn &lt;template&gt;</c>.
/// </summary>
public sealed class SpawnCommand : ICommandExecutor
{
    private readonly ILogger _logger = Log.ForContext<SpawnCommand>();
    private readonly INpcService _npcs;
    private readonly IMobileTemplateService _templates;
    private readonly ITargetService _targets;
    private readonly ILocalizationService? _localization;

    public SpawnCommand(
        INpcService npcs,
        IMobileTemplateService templates,
        ITargetService targets,
        ILocalizationService? localization = null
    )
    {
        _localization = localization;
        _npcs = npcs;
        _templates = templates;
        _targets = targets;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("spawn works in game only.");

            return;
        }

        if (context.Arguments.Length != 1)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "spawn <template>"));

            return;
        }

        var templateId = context.Arguments[0];

        if (!_templates.TryGet(templateId, out _))
        {
            context.PrintError(_localization.Text(CommandMessages.UnknownMobileTemplate, "Unknown mobile template: {0}", templateId));

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
            var npc = await _npcs.SpawnAsync(templateId, target.Map, target.Location, cancellationToken: context.CancellationToken);
            var spot = npc.Location;
            context.Print(
                _localization.Text(
                    CommandMessages.Spawned,
                    "Spawned {0} ({1}) at {2} ({3}, {4}, {5}).",
                    npc.Name,
                    npc.Id,
                    npc.Map,
                    spot.X,
                    spot.Y,
                    spot.Z
                )
            );
        }
        catch (Exception exception)
        {
            // The exception is English and technical: the GM gets the reason from the log.
            _logger.Error(exception, "Spawning {Template} failed", templateId);
            context.PrintError(_localization.Text(CommandMessages.SpawnFailed, "The spawn failed. Check the server logs."));
        }
    }
}
