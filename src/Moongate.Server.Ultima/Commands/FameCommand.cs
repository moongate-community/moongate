using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>fame &lt;0..32000&gt;</c>: sets the fame of the character or NPC a game master targets, which its paperdoll
///     title follows.
/// </summary>
public sealed class FameCommand : ICommandExecutor
{
    public const int MaximumFame = 32000;

    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public FameCommand(
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
            "fame",
            0,
            MaximumFame,
            _targets,
            _mobiles,
            _loop,
            _localization,
            CommandMessages.FameSet,
            "{0} now has {1} fame.",
            (mobile, fame) => mobile.Fame = fame
        );
    }
}
