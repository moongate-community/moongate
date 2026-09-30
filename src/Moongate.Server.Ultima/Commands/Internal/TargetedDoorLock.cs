using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands.Internal;

/// <summary>
///     Locks or unlocks the door a game master targets, and the door linked to it: the prop <c>locked</c> that
///     <c>scripts/items/door.lua</c> reads. A door is an item whose template runs the door script.
/// </summary>
internal static class TargetedDoorLock
{
    public const string LockedProp = "locked";
    private const string DoorScript = "door";
    private const string LinkProp = "door.link";

    public static async Task RunAsync(
        CommandContext context,
        string command,
        bool locked,
        ITargetService targets,
        IItemService items,
        IItemTemplateService templates,
        IGameLoopService loop,
        ILocalizationService? localization
    )
    {
        if (context.Session is not { } session)
        {
            context.PrintError($"{command} works in game only.");

            return;
        }

        var target = await targets.RequestAsync(session, TargetCursorType.Object, TargetFlagsType.Neutral, context.CancellationToken);

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        var done = false;
        var work = new LoopActionWorkItem(
            () =>
            {
                if (!TryGetDoor(items, templates, target.Serial, out var door))
                {
                    return;
                }

                Apply(door, locked);

                if (door.Props?.GetValueOrDefault(LinkProp) is long link &&
                    link is > 0 and <= uint.MaxValue &&
                    TryGetDoor(items, templates, new Serial((uint)link), out var linked))
                {
                    Apply(linked, locked);
                }

                done = true;
            }
        );
        await loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        context.Print(
            !done ? localization.Text(CommandMessages.NotADoor, "That is not a door.") :
            locked ? localization.Text(CommandMessages.DoorLocked, "The door is now locked.") :
            localization.Text(CommandMessages.DoorUnlocked, "The door is now unlocked.")
        );
    }

    private static bool TryGetDoor(
        IItemService items,
        IItemTemplateService templates,
        Serial serial,
        [NotNullWhen(true)] out ItemEntity? door
    )
    {
        if (items.TryGet(serial, out var item) &&
            templates.TryGet(item.TemplateId, out var template) &&
            template.ScriptId == DoorScript)
        {
            door = item;

            return true;
        }

        door = null;

        return false;
    }

    private static void Apply(ItemEntity door, bool locked)
    {
        if (locked)
        {
            door.SetProp(LockedProp, true);
        }
        else
        {
            door.RemoveProp(LockedProp);
        }
    }
}
