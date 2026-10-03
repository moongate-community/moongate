using Moongate.Core.Geometry;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Puts a moongate at the game master's feet: <c>moongate &lt;x&gt;,&lt;y&gt;,&lt;z&gt;</c> leads to a place of its
///     own map, <c>moongate &lt;x&gt;,&lt;y&gt;,&lt;z&gt; &lt;map&gt;</c> to one of another. The gate keeps the place
///     in its props, read by <c>scripts/items/moongate.lua</c>.
/// </summary>
public sealed class MoongateCommand : ICommandExecutor
{
    public const string MoongateTemplate = "moongate";

    public const string XProp = "teleport.x";
    public const string YProp = "teleport.y";
    public const string ZProp = "teleport.z";
    public const string MapProp = "teleport.map";

    private const string UsageText = "moongate <x>,<y>,<z> [map]";

    private readonly IMobileService _mobiles;
    private readonly ISectorService _sectors;
    private readonly IItemService _items;
    private readonly IItemFactoryService _factory;
    private readonly IWorldViewService _view;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public MoongateCommand(
        IMobileService mobiles,
        ISectorService sectors,
        IItemService items,
        IItemFactoryService factory,
        IWorldViewService view,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _mobiles = mobiles;
        _sectors = sectors;
        _items = items;
        _factory = factory;
        _view = view;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("moongate works in game only.");

            return;
        }

        if (!PlaceArgument.TryParse(context.Arguments, character.Map, out var map, out var destination))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        // Where the game master stands now: the gate is saved off the loop, and they may walk meanwhile.
        MapType? gateMap = null;
        var feet = default(Point3D);
        await OnLoopAsync(
            () =>
            {
                if (_sectors.IsInside(map, destination.X, destination.Y))
                {
                    gateMap = character.Map;
                    feet = character.Location;
                }
            },
            context.CancellationToken
        );

        if (gateMap is not { } here)
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.MoongateRefused,
                    "No moongate can lead there: {0} is not loaded or the spot is outside it.",
                    EnumNameUtils.Format(map)
                )
            );

            return;
        }

        // Saved first: the database gives the gate its serial.
        var gate = _factory.Create(MoongateTemplate);
        gate.SetProp(XProp, (long)destination.X);
        gate.SetProp(YProp, (long)destination.Y);
        gate.SetProp(ZProp, (long)destination.Z);
        gate.SetProp(MapProp, (long)map);
        // As ModernUO's Moongate: the gate glows.
        gate.SetProp(ItemModule.LightProp, EnumNameUtils.Format(LightType.Circle300));
        gate.PlaceOnGround(here, feet);
        await _factory.SaveAsync(gate, context.CancellationToken);
        await OnLoopAsync(
            () =>
            {
                _items.Add([gate]);
                _view.ItemAppeared(gate);
            },
            CancellationToken.None
        );

        context.Print(
            _localization.Text(
                CommandMessages.MoongateCreated,
                "A moongate to {0} ({1}, {2}, {3}) is at your feet.",
                EnumNameUtils.Format(map),
                destination.X,
                destination.Y,
                destination.Z
            )
        );
    }

    private async Task OnLoopAsync(Action action, CancellationToken cancellationToken)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, cancellationToken);
        await work.Completion;
    }
}
