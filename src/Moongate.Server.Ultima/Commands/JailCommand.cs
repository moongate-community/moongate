using Lua;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Opens the gump of the jail: it lists the cells of <c>data/jail.toml</c> with who is inside, takes the game
///     master into one, and, once a character is picked with its target button, a player or an NPC, sends it to a
///     free cell for the days typed or releases it. <c>jail &lt;name&gt;</c> opens it on the player of that name, in
///     the world or not: one who is offline is jailed at its next login. Several players of one name are listed in
///     the gump.
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

        // A name of several words comes as several arguments.
        var name = string.Join(' ', context.Arguments).Trim();
        IReadOnlyList<JailCandidate> found = [];

        if (name.Length > 0)
        {
            found = await _jail.FindAsync(name, context.CancellationToken);

            if (found.Count == 0)
            {
                context.PrintError(_localization.Text(CommandMessages.JailNobodyNamed, "No character is named {0}.", name));

                return;
            }
        }

        if (!await OpenGumpAsync(character, found, context.CancellationToken))
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
    private async Task<bool> OpenGumpAsync(
        MobileEntity character,
        IReadOnlyList<JailCandidate> found,
        CancellationToken cancellationToken
    )
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
                args["days"] = "1";

                if (found.Count == 1)
                {
                    args["target"] = (long)found[0].Id.Value;
                    args["name"] = found[0].Name;
                }
                else if (found.Count > 1)
                {
                    // Several of one name: the game master picks which from the gump.
                    args["candidates"] = Candidates(found);
                }

                opened = _gumps.Open(character.Id.Value, GumpId, args);
            }
        );
        await _loop.PostAsync(open, cancellationToken);
        await open.Completion;

        return opened;
    }

    private static LuaTable Candidates(IReadOnlyList<JailCandidate> found)
    {
        var candidates = new LuaTable();

        for (var index = 0; index < found.Count; index++)
        {
            var entry = new LuaTable();
            entry["serial"] = (long)found[index].Id.Value;
            entry["name"] = found[index].Name;
            entry["account"] = found[index].Account;
            candidates[index + 1] = entry;
        }

        return candidates;
    }
}
