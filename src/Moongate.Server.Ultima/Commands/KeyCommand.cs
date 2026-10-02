using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>key</c>: puts in the game master's backpack a key that opens the door they target (and its linked door),
///     giving the door a key number first if it has none.
/// </summary>
public sealed class KeyCommand : ICommandExecutor
{
    public const string KeyTemplate = "0x1010_iron_key";

    private static readonly Point2D InBackpack = new(44, 65);

    private readonly ITargetService _targets;
    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly IItemFactoryService _factory;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public KeyCommand(
        ITargetService targets,
        IItemService items,
        IItemTemplateService templates,
        IItemFactoryService factory,
        IPacketSendService sender,
        ITooltipService tooltips,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _items = items;
        _templates = templates;
        _factory = factory;
        _sender = sender;
        _tooltips = tooltips;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("key works in game only.");

            return;
        }

        var target = await _targets.RequestAsync(session, TargetCursorType.Object, TargetFlagsType.Neutral, context.CancellationToken);

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        long? value = null;
        Serial? backpack = null;
        await OnLoopAsync(
            () =>
            {
                if (DoorKeys.TryGetDoor(_items, _templates, target.Serial, out var door))
                {
                    value = DoorKeys.EnsureKeyValue(door, DoorKeys.LinkedDoor(_items, _templates, door));
                    backpack = _items.GetWorn(session.CharacterId).FirstOrDefault(item => item.Layer == LayerType.Backpack)?.Id;
                }
            },
            context.CancellationToken
        );

        if (value is not { } keyValue)
        {
            context.Print(_localization.Text(CommandMessages.NotADoor, "That is not a door."));

            return;
        }

        if (backpack is not { } container)
        {
            context.PrintError(_localization.Text(CommandMessages.NoBackpack, "You have no backpack."));

            return;
        }

        // Saved first: the database gives the key its serial.
        var key = _factory.Create(KeyTemplate);
        key.SetProp(DoorKeys.KeyValueProp, keyValue);
        key.PutInContainer(container, InBackpack);
        await _factory.SaveAsync(key, context.CancellationToken);
        await OnLoopAsync(
            () =>
            {
                // On the loop, where the backpack's contents are known: the key takes a free grid slot.
                key.GridIndex = ContainerSlotUtils.FirstFree(_items.GetContents(container));
                _items.Add([key]);
                _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(key, session.UsesContainerGrid()));
                _sender.TrySend(session.SessionId, _tooltips.Info(key));
            },
            CancellationToken.None
        );

        context.Print(_localization.Text(CommandMessages.KeyCreated, "A key for the door is in your backpack."));
    }

    private async Task OnLoopAsync(Action action, CancellationToken cancellationToken)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, cancellationToken);
        await work.Completion;
    }
}
