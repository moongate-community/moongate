using Moongate.Core.Geometry;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
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

        if (!PlaceArgument.TryParse(context.Arguments, character.Map, out var map, out var location))
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
}
