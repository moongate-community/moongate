using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>mark_rune</c>: marks the recall rune you target with the place where you stand, for the Recall spell to carry
///     its caster to. The rune is named for the region it was marked in, when the region has a name.
/// </summary>
public sealed class MarkRuneCommand : ICommandExecutor
{
    public const string RuneTemplate = "recall_rune";

    private readonly ITargetService _targets;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IRegionService _regions;
    private readonly IItemHandlingService _handling;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public MarkRuneCommand(
        ITargetService targets,
        IItemService items,
        IMobileService mobiles,
        IRegionService regions,
        IItemHandlingService handling,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _items = items;
        _mobiles = mobiles;
        _regions = regions;
        _handling = handling;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("mark_rune works in game only.");

            return;
        }

        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Neutral,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        string? place = null;
        var work = new LoopActionWorkItem(() =>
            {
                if (!_items.TryGet(target.Serial, out var rune) ||
                    rune.TemplateId != RuneTemplate ||
                    !_mobiles.TryGet(session.CharacterId, out var gm))
                {
                    return;
                }

                var here = gm.Location;
                var region = _regions.Find(gm.Map, here);
                place = region?.RuneName ?? region?.Name ?? gm.Map.ToString();
                rune.SetProp("rune.marked", true);
                rune.SetProp("rune.x", (long)here.X);
                rune.SetProp("rune.y", (long)here.Y);
                rune.SetProp("rune.z", (long)here.Z);
                rune.SetProp("rune.map", (long)gm.Map);
                rune.Name = $"a recall rune for {place}";
                _handling.Refresh(rune);
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        context.Print(
            place is null
                ? _localization.Text(CommandMessages.NotARune, "That is not a recall rune.")
                : _localization.Text(CommandMessages.RuneMarked, "The rune is marked for {0}.", place)
        );
    }
}
