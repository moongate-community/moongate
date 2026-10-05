using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>animate &lt;action&gt;</c>: makes the character or NPC a game master targets play an action of its body, seen by
///     everyone around, as ModernUO's <c>[animate</c>: to try an animation, such as 21, a human falling dead.
/// </summary>
public sealed class AnimateCommand : ICommandExecutor
{
    /// <summary>
    ///     The frames asked of the client: enough for the longest actions, such as a death.
    /// </summary>
    public const int Frames = 10;

    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public AnimateCommand(
        ITargetService targets,
        IMobileService mobiles,
        IWorldViewService view,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _mobiles = mobiles;
        _view = view;
        _loop = loop;
        _localization = localization;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        return TargetedMobileValue.RunAsync(
            context,
            "animate",
            0,
            ushort.MaxValue,
            _targets,
            _mobiles,
            _loop,
            _localization,
            CommandMessages.Animated,
            "{0} plays action {1}.",
            (mobile, action) => _view.MobileAnimated(mobile, action, Frames, 1)
        );
    }
}
