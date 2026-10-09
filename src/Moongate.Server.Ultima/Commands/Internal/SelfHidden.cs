using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Effects;

namespace Moongate.Server.Ultima.Commands.Internal;

/// <summary>
///     What <c>hide</c> and <c>unhide</c> share: they hide or show the game master that types them, in a puff of smoke
///     with its sound, as ModernUO's <c>[Hide]</c> and <c>[Unhide]</c>.
/// </summary>
internal static class SelfHidden
{
    /// <summary>
    ///     The sound of the puff.
    /// </summary>
    public const int Sound = 0x228;

    public static async Task RunAsync(
        CommandContext context,
        bool hidden,
        string commandName,
        IMobileService mobiles,
        IMobileStateService state,
        IGameLoopService loop,
        IEffectService? effects,
        ISpeechService? speech,
        ILocalizationService? localization,
        int doneMessage,
        string doneEnglish
    )
    {
        if (context.Session is not { } session)
        {
            context.PrintError($"{commandName} works in game only.");

            return;
        }

        var changed = false;
        var work = new LoopActionWorkItem(() =>
            {
                if (!mobiles.TryGet(session.CharacterId, out var mobile) || mobile.Hidden == hidden)
                {
                    return;
                }

                state.SetHidden(mobile, hidden);
                effects?.PlayAt(mobile.Map, mobile.Location, new EffectOptions { Graphic = (int)EffectGraphicType.Smoke });
                speech?.PlaySound(mobile.Map, mobile.Location, Sound);
                changed = true;
            }
        );
        await loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        context.Print(
            changed
                ? localization.Text(doneMessage, doneEnglish)
                : localization.Text(CommandMessages.AlreadyThatWay, "You are already that way.")
        );
    }
}
