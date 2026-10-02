using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Prints the music where the game master stands, <c>music</c>, or plays a track to them until their next region
///     change, <c>music &lt;track&gt;</c>, by <see cref="MusicType" /> name such as <c>tavern04</c>.
/// </summary>
public sealed class MusicCommand : ICommandExecutor
{
    private const string UsageText = "music [track]";

    private readonly IMusicService _music;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public MusicCommand(
        IMusicService music,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _music = music;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("music works in game only.");

            return;
        }

        if (context.Arguments.Length == 0)
        {
            context.Print(
                _localization.Text(CommandMessages.MusicHere, "Music here: {0}.", EnumNameUtils.Format(_music.MusicOf(character)))
            );

            return;
        }

        if (context.Arguments.Length != 1 || !EnumNameUtils.TryParse<MusicType>(context.Arguments[0], out var music))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        // The music service is driven by the loop; commands run off it.
        var played = false;
        var play = new LoopActionWorkItem(() => played = _music.Play(character, music));
        await _loop.PostAsync(play, context.CancellationToken);
        await play.Completion;

        if (!played)
        {
            context.PrintError(_localization.Text(CommandMessages.MusicNotPlayed, "{0} could not play.", EnumNameUtils.Format(music)));

            return;
        }

        context.Print(_localization.Text(CommandMessages.MusicPlaying, "Playing {0}.", EnumNameUtils.Format(music)));
    }
}
