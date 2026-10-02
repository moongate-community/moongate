using System.Globalization;
using Moongate.Core.Geometry;
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
///     Takes the game master to a place: <c>go &lt;x&gt;,&lt;y&gt;,&lt;z&gt;</c> on its own map, or
///     <c>go &lt;x&gt;,&lt;y&gt;,&lt;z&gt; &lt;map&gt;</c> on another. The numbers may be split by commas or by spaces.
/// </summary>
public sealed class GoCommand : ICommandExecutor
{
    private const string UsageText = "go <x>,<y>,<z> [map]";

    private static readonly char[] Separators = [',', ' '];

    private readonly ITeleportService _teleports;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public GoCommand(
        ITeleportService teleports,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _teleports = teleports;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("go works in game only.");

            return;
        }

        if (!TryParse(context.Arguments, character.Map, out var map, out var location))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

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

    // Three numbers, split by commas or spaces, then at most the name of a map.
    private static bool TryParse(IReadOnlyList<string> arguments, MapType own, out MapType map, out Point3D location)
    {
        map = own;
        location = default;
        var parts = string.Join(' ', arguments).Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is < 3 or > 4 ||
            !TryParseNumber(parts[0], 0, ushort.MaxValue, out var x) ||
            !TryParseNumber(parts[1], 0, ushort.MaxValue, out var y) ||
            !TryParseNumber(parts[2], sbyte.MinValue, sbyte.MaxValue, out var z))
        {
            return false;
        }

        location = new Point3D(x, y, z);

        // A map is named, never numbered: a fourth number is a mistake.
        return parts.Length == 3 ||
               !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
               EnumNameUtils.TryParse(parts[3], out map) &&
               Enum.IsDefined(map);
    }

    private static bool TryParseNumber(string text, int minimum, int maximum, out int value)
    {
        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) &&
               value >= minimum &&
               value <= maximum;
    }
}
