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

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Opens the gump of the jail: it lists the cells of <c>data/jail.toml</c> with who is inside, takes the game
///     master into one, and, once a character is picked with its target button, a player or an NPC, sends it to a
///     free cell for the days typed or releases it.
/// </summary>
public sealed class JailCommand : ICommandExecutor
{
    public const string GumpId = "jail_sentence";

    private readonly IJailService _jail;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;
    private readonly GumpModule? _gumps;

    public JailCommand(
        IJailService jail,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null,
        GumpModule? gumps = null
    )
    {
        _jail = jail;
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

        if (!await OpenGumpAsync(character, context.CancellationToken))
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
    private async Task<bool> OpenGumpAsync(MobileEntity character, CancellationToken cancellationToken)
    {
        if (_gumps is null)
        {
            return false;
        }

        var opened = false;
        var open = new LoopActionWorkItem(
            () =>
            {
                // No target yet: the game master picks one from the gump.
                var args = new LuaTable();
                args["days"] = "1";
                opened = _gumps.Open(character.Id.Value, GumpId, args);
            }
        );
        await _loop.PostAsync(open, cancellationToken);
        await open.Completion;

        return opened;
    }
}
