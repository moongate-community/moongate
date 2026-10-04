using Lua;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Opens the gump of the jail on the character the game master targets, a player or an NPC: the gump lists the
///     cells of <c>data/jail.toml</c>, sends the character to a free one for the days typed, or releases it.
/// </summary>
public sealed class JailCommand : ICommandExecutor
{
    public const string GumpId = "jail_sentence";

    private readonly IJailService _jail;
    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;
    private readonly GumpModule? _gumps;

    public JailCommand(
        IJailService jail,
        ITargetService targets,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null,
        GumpModule? gumps = null
    )
    {
        _jail = jail;
        _targets = targets;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
        _gumps = gumps;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("jail works in game only.");

            return;
        }

        if (!_jail.IsEnabled)
        {
            context.PrintError(
                _localization.Text(CommandMessages.JailNotSetUp, "The jail is not set up: data/jail.toml is missing.")
            );

            return;
        }

        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Neutral,
            context.CancellationToken
        );

        if (target.Kind == TargetResultType.Canceled)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        if (target.Kind != TargetResultType.Object ||
            target.Serial.IsItem ||
            !_mobiles.TryGet(target.Serial, out var prisoner))
        {
            context.PrintError(_localization.Text(CommandMessages.NotACharacter, "That is not a character."));

            return;
        }

        if (!await OpenGumpAsync(character, prisoner, context.CancellationToken))
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.JailGumpMissing,
                    "The jail gump is missing: templates/gumps/jail_sentence.xml."
                )
            );
        }
    }

    // The gump opens on the loop, where its script fills the cells.
    private async Task<bool> OpenGumpAsync(MobileEntity character, MobileEntity prisoner, CancellationToken cancellationToken)
    {
        if (_gumps is null)
        {
            return false;
        }

        var opened = false;
        var open = new LoopActionWorkItem(
            () =>
            {
                var args = new LuaTable();
                args["target"] = (long)prisoner.Id.Value;
                args["name"] = prisoner.Name;
                args["days"] = "1";
                opened = _gumps.Open(character.Id.Value, GumpId, args);
            }
        );
        await _loop.PostAsync(open, cancellationToken);
        await open.Completion;

        return opened;
    }
}
