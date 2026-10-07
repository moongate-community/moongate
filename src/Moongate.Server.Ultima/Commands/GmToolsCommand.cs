using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Opens the gump of the game master's tools, <c>gmtools</c>: a sidebar of tools and the commands of the selected
///     one, the weather first. The gump is <c>templates/gumps/gmtools.xml</c> and its script
///     <c>scripts/gumps/gmtools.lua</c>.
/// </summary>
public sealed class GmToolsCommand : ICommandExecutor
{
    private const string GumpId = "gmtools";

    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;
    private readonly GumpModule? _gumps;

    public GmToolsCommand(
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null,
        GumpModule? gumps = null
    )
    {
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
        _gumps = gumps;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("gmtools works in game only.");

            return;
        }

        var opened = false;

        if (_gumps is not null)
        {
            // The gump opens on the loop, where its script fills the columns.
            var open = new LoopActionWorkItem(() => opened = _gumps.Open(character.Id.Value, GumpId));
            await _loop.PostAsync(open, context.CancellationToken);
            await open.Completion;
        }

        if (!opened)
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.GmToolsGumpMissing,
                    "The gmtools gump is missing: templates/gumps/gmtools.xml."
                )
            );
        }
    }
}
