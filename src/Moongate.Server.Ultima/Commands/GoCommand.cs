using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Takes the game master to a place. <c>go</c> alone opens the gump of the named places of
///     <c>data/locations.toml</c> on its map; <c>go &lt;place&gt;</c> goes to the one the words name, such as
///     <c>go covetous entrance</c>; <c>go &lt;x&gt;,&lt;y&gt;,&lt;z&gt; [map]</c> goes to a spot, the numbers split by
///     commas or by spaces.
/// </summary>
public sealed class GoCommand : ICommandExecutor
{
    private const string UsageText = "go [<x>,<y>,<z> [map] | <place>]";
    private const string GumpId = "go";

    // How many places of the same name are listed.
    private const int ListedPlaces = 10;

    private readonly ITeleportService _teleports;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;
    private readonly ILocationService? _locations;
    private readonly GumpModule? _gumps;

    public GoCommand(
        ITeleportService teleports,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null,
        ILocationService? locations = null,
        GumpModule? gumps = null
    )
    {
        _teleports = teleports;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
        _locations = locations;
        _gumps = gumps;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("go works in game only.");

            return;
        }

        if (context.Arguments.Length == 0)
        {
            if (!await OpenGumpAsync(character, context.CancellationToken))
            {
                context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));
            }

            return;
        }

        // Numbers are a spot; anything else is the name of a place.
        if (_locations is null || context.Arguments[0][0] is '-' or (>= '0' and <= '9'))
        {
            if (!PlaceArgument.TryParse(context.Arguments, character.Map, out var map, out var location))
            {
                context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

                return;
            }

            await TeleportAsync(context, character, map, location);

            return;
        }

        var name = string.Join(' ', string.Join(' ', context.Arguments).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        IReadOnlyList<NamedLocation> places = [];
        // The places are worked out on the loop.
        var find = new LoopActionWorkItem(() => places = _locations.Find(name, character.Map));
        await _loop.PostAsync(find, context.CancellationToken);
        await find.Completion;

        switch (places.Count)
        {
            case 0:
                context.PrintError(
                    _localization.Text(CommandMessages.GoNoPlace, "No place is named {0}; go alone lists them.", name)
                );

                break;
            case 1:
                await TeleportAsync(context, character, places[0].Map, places[0].Location);

                break;
            default:
                List(context, name, places);

                break;
        }
    }

    // Opens the gump on the character's map, or on the list of the maps when that one has no places.
    private async Task<bool> OpenGumpAsync(MobileEntity character, CancellationToken cancellationToken)
    {
        if (_locations is null || _gumps is null)
        {
            return false;
        }

        var opened = false;
        var open = new LoopActionWorkItem(
            () =>
            {
                if (_locations.GetNode("") is not { Categories.Count: > 0 })
                {
                    return;
                }

                var args = new LuaTable();
                args["path"] = _locations.GetNode(character.Map.ToString())?.Path ?? "";
                opened = _gumps.Open(character.Id.Value, GumpId, args);
            }
        );
        await _loop.PostAsync(open, cancellationToken);
        await open.Completion;

        return opened;
    }

    private async Task TeleportAsync(CommandContext context, MobileEntity character, MapType map, Point3D location)
    {
        // The world is changed by the loop; commands run off it.
        var arrived = false;
        var teleport = new LoopActionWorkItem(() => arrived = _teleports.Teleport(character, map, location));
        await _loop.PostAsync(teleport, context.CancellationToken);
        await teleport.Completion;

        if (!arrived)
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.GoRefused,
                    "You cannot go there: {0} is not loaded or the spot is outside it.",
                    EnumNameUtils.Format(map)
                )
            );
        }
    }

    private void List(CommandContext context, string name, IReadOnlyList<NamedLocation> places)
    {
        context.Print(
            _localization.Text(
                CommandMessages.GoSeveralPlaces,
                "{0} places are named {1}; add words of the category, such as go covetous entrance:",
                places.Count,
                name
            )
        );

        foreach (var place in places.Take(ListedPlaces))
        {
            context.Print(place.Category.Length == 0 ? $"{place.Map}: {place.Name}" : $"{place.Map}: {place.Category}/{place.Name}");
        }

        if (places.Count > ListedPlaces)
        {
            context.Print(_localization.Text(CommandMessages.GoMorePlaces, "... and {0} more.", places.Count - ListedPlaces));
        }
    }
}
