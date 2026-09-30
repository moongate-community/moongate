using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Weather;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Prints the weather where the game master stands, <c>weather</c>, or forces it on that weather profile until the
///     next game hour, <c>weather &lt;none|rain|snow|storm&gt;</c>.
/// </summary>
public sealed class WeatherCommand : ICommandExecutor
{
    private const string UsageText = "weather [none|rain|snow|storm]";

    private readonly IWeatherService _weather;
    private readonly IMobileService _mobiles;
    private readonly ILocalizationService? _localization;

    public WeatherCommand(IWeatherService weather, IMobileService mobiles, ILocalizationService? localization = null)
    {
        _localization = localization;
        _weather = weather;
        _mobiles = mobiles;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("weather works in game only.");

            return Task.CompletedTask;
        }

        var profile = _weather.ProfileOf(character);

        if (context.Arguments.Length == 0)
        {
            var state = _weather.StateOf(profile);
            context.Print(
                _localization.Text(
                    CommandMessages.WeatherHere,
                    "Weather here ({0}): {1}, density {2}, temperature {3}.",
                    profile,
                    EnumNameUtils.Format(state.Kind),
                    state.Density,
                    state.Temperature
                )
            );

            return Task.CompletedTask;
        }

        if (context.Arguments.Length != 1 || !EnumNameUtils.TryParse<WeatherKindType>(context.Arguments[0], out var kind))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return Task.CompletedTask;
        }

        _weather.Force(profile, kind);
        context.Print(
            _localization.Text(
                CommandMessages.WeatherForced,
                "The weather of {0} is now {1} until the next hour.",
                profile,
                EnumNameUtils.Format(kind)
            )
        );

        return Task.CompletedTask;
    }
}
