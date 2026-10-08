using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>unhide</c>: shows the game master that types it again, in a puff of smoke.
/// </summary>
public sealed class UnhideCommand : ICommandExecutor
{
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly IGameLoopService _loop;
    private readonly IEffectService? _effects;
    private readonly ISpeechService? _speech;
    private readonly ILocalizationService? _localization;

    public UnhideCommand(
        IMobileService mobiles,
        IMobileStateService state,
        IGameLoopService loop,
        IEffectService? effects = null,
        ISpeechService? speech = null,
        ILocalizationService? localization = null
    )
    {
        _mobiles = mobiles;
        _state = state;
        _loop = loop;
        _effects = effects;
        _speech = speech;
        _localization = localization;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        return SelfHidden.RunAsync(
            context,
            false,
            "unhide",
            _mobiles,
            _state,
            _loop,
            _effects,
            _speech,
            _localization,
            CommandMessages.UnhideDone,
            "You are visible again."
        );
    }
}
