using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Prints the season where the game master stands and its map's, <c>season</c>, or sets the season of its map until
///     the restart, <c>season &lt;season&gt;</c>; <c>season auto</c> gives the map back its <c>maps.toml</c> season.
/// </summary>
public sealed class SeasonCommand : ICommandExecutor
{
    private const string UsageText = "season [spring|summer|fall|winter|desolation|auto]";
    private const string Auto = "auto";

    private readonly ISeasonService _seasons;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public SeasonCommand(
        ISeasonService seasons,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _seasons = seasons;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("season works in game only.");

            return;
        }

        var map = character.Map;

        if (context.Arguments.Length == 0)
        {
            context.Print(
                _localization.Text(
                    CommandMessages.SeasonHere,
                    "Season here: {0}; {1}: {2}.",
                    EnumNameUtils.Format(_seasons.SeasonOf(character)),
                    EnumNameUtils.Format(map),
                    EnumNameUtils.Format(_seasons.SeasonOf(map))
                )
            );

            return;
        }

        SeasonType? season = null;

        if (context.Arguments.Length != 1 ||
            !string.Equals(context.Arguments[0], Auto, StringComparison.OrdinalIgnoreCase) &&
            !TryParse(context.Arguments[0], out season))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        // The season service is driven by the loop; commands run off it.
        var set = new LoopActionWorkItem(() => _seasons.SetOverride(map, season));
        await _loop.PostAsync(set, context.CancellationToken);
        await set.Completion;
        context.Print(
            _localization.Text(
                CommandMessages.SeasonSet,
                "Season of {0}: {1}.",
                EnumNameUtils.Format(map),
                EnumNameUtils.Format(_seasons.SeasonOf(map))
            )
        );
    }

    private static bool TryParse(string text, out SeasonType? season)
    {
        season = null;

        if (!EnumNameUtils.TryParse<SeasonType>(text, out var value) || !Enum.IsDefined(value))
        {
            return false;
        }

        season = value;

        return true;
    }
}
