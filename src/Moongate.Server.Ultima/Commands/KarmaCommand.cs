using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>karma &lt;-32000..32000&gt;</c>: sets the karma of the character or NPC a game master targets, which its
///     paperdoll
///     title follows.
/// </summary>
public sealed class KarmaCommand : ICommandExecutor
{
    public const int MaximumKarma = 32000;

    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public KarmaCommand(
        ITargetService targets,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        return TargetedMobileValue.RunAsync(
            context,
            "karma",
            -MaximumKarma,
            MaximumKarma,
            _targets,
            _mobiles,
            _loop,
            _localization,
            CommandMessages.KarmaSet,
            "{0} now has {1} karma.",
            (mobile, karma) => mobile.Karma = karma
        );
    }
}
