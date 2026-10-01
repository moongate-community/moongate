using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Prints the game time where the player stands, <c>time</c>, which depends on the map and moves one minute later
///     every 16 tiles east, and the phases of Trammel and Felucca, as ModernUO's spyglass.
/// </summary>
public sealed class TimeCommand : ICommandExecutor
{
    private readonly IClockService _clock;
    private readonly IMobileService _mobiles;
    private readonly ILocalizationService? _localization;

    public TimeCommand(IClockService clock, IMobileService mobiles, ILocalizationService? localization = null)
    {
        _clock = clock;
        _mobiles = mobiles;
        _localization = localization;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("time works in game only.");

            return Task.CompletedTask;
        }

        if (context.Arguments.Length != 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "time"));

            return Task.CompletedTask;
        }

        var time = _clock.GetTime(character.Map, character.Location.X);
        context.Print(_localization.Text(CommandMessages.TimeHere, "Game time here: {0}.", $"{time.Hours:00}:{time.Minutes:00}"));
        context.Print(
            _localization.Text(
                CommandMessages.TimeMoons,
                "Moons: Trammel {0}, Felucca {1}.",
                PhaseName(_clock.GetMoonPhase(MapType.Trammel, character.Location.X)),
                PhaseName(_clock.GetMoonPhase(MapType.Felucca, character.Location.X))
            )
        );

        return Task.CompletedTask;
    }

    private static string PhaseName(MoonPhaseType phase)
    {
        return EnumNameUtils.Format(phase).Replace('_', ' ');
    }
}
