using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Spawns an NPC from a mobile template where the game master targets: <c>spawn &lt;template&gt;</c>.
/// </summary>
public sealed class SpawnCommand : ICommandExecutor
{
    private const string Usage = "Usage: spawn <template>";

    private readonly INpcService _npcs;
    private readonly IMobileTemplateService _templates;
    private readonly ITargetService _targets;

    public SpawnCommand(INpcService npcs, IMobileTemplateService templates, ITargetService targets)
    {
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
            context.PrintError(Usage);

            return;
        }

        var templateId = context.Arguments[0];

        if (!_templates.TryGet(templateId, out _))
        {
            context.PrintError("Unknown mobile template: {0}", templateId);

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
            context.Print("Canceled.");

            return;
        }

        try
        {
            var npc = await _npcs.SpawnAsync(templateId, target.Map, target.Location, context.CancellationToken);
            var spot = npc.Location;
            context.Print(
                "Spawned {0} ({1}) at {2} ({3}, {4}, {5}).",
                npc.Name,
                npc.Id,
                npc.Map,
                spot.X,
                spot.Y,
                spot.Z
            );
        }
        catch (Exception exception)
        {
            context.PrintError("Spawn failed: {0}", exception.Message);
        }
    }
}
