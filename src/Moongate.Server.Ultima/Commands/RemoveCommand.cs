using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Removes what the game master targets: an NPC, or an item lying on the ground with everything inside it. Players
///     and items that are carried, worn or inside a container are refused.
/// </summary>
public sealed class RemoveCommand : ICommandExecutor
{
    private readonly INpcService _npcs;
    private readonly ITargetService _targets;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public RemoveCommand(
        INpcService npcs,
        ITargetService targets,
        IItemService items,
        IWorldViewService view,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _localization = localization;
        _npcs = npcs;
        _targets = targets;
        _items = items;
        _view = view;
        _loop = loop;
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

        if (target.Serial.IsItem)
        {
            if (await RemoveGroundItemAsync(target.Serial, context.CancellationToken))
            {
                context.Print(_localization.Text(CommandMessages.Removed, "Removed {0}.", target.Serial));
            }
            else
            {
                context.Print(
                    _localization.Text(
                        CommandMessages.RemoveOnlyGroundItems,
                        "Only an item lying on the ground can be removed."
                    )
                );
            }

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

    // On the game loop, where the items live: off every screen, then forgotten with what it holds; the next world save
    // deletes the rows.
    private async Task<bool> RemoveGroundItemAsync(Serial serial, CancellationToken cancellationToken)
    {
        var removed = false;
        var work = new LoopActionWorkItem(() =>
            {
                if (!_items.TryGet(serial, out var item) || !_items.IsLyingOnGround(item))
                {
                    return;
                }

                _view.ItemDisappeared(item);
                AbsorbContents(item);
                _items.Absorb(item);
                removed = true;
            }
        );
        await _loop.PostAsync(work, cancellationToken);
        await work.Completion;

        return removed;
    }

    private void AbsorbContents(ItemEntity container)
    {
        foreach (var content in _items.GetContents(container.Id))
        {
            AbsorbContents(content);
            _items.Absorb(content);
        }
    }
}
