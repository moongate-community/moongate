using System.Globalization;
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
///     Sets a whole number on the mobile a game master targets, such as its fame: the value is checked before the target
///     cursor opens, and the mobile changes on the game loop.
/// </summary>
internal static class TargetedMobileValue
{
    public static async Task RunAsync(
        CommandContext context,
        string command,
        int minimum,
        int maximum,
        ITargetService targets,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization,
        int doneMessage,
        string doneEnglish,
        Action<MobileEntity, int> apply
    )
    {
        if (context.Session is not { } session)
        {
            context.PrintError($"{command} works in game only.");

            return;
        }

        if (context.Arguments.Length != 1 ||
            !int.TryParse(
                context.Arguments[0],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value
            ) ||
            value < minimum ||
            value > maximum)
        {
            context.PrintError(
                localization.Text(CommandMessages.Usage, "Usage: {0}", $"{command} <{minimum}..{maximum}>")
            );

            return;
        }

        var target = await targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Neutral,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        string? name = null;
        var work = new LoopActionWorkItem(() =>
            {
                if (mobiles.TryGet(target.Serial, out var mobile))
                {
                    apply(mobile, value);
                    name = mobile.Name;
                }
            }
        );
        await loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        context.Print(
            name is null
                ? localization.Text(CommandMessages.NotAMobile, "That is not a character or an NPC.")
                : localization.Text(doneMessage, doneEnglish, name, value)
        );
    }
}
