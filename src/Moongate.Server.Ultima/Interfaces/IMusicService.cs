using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Plays each player the music of the region it stands in (0x6D), or its map's where the region has none, and stops
///     it where neither has any. It follows the players through the region changes, sending only when the track
///     changes, and nothing before a player's login completes.
/// </summary>
public interface IMusicService : IMoongateStartupService, IRegionChangeListener
{
    /// <summary>
    ///     Gets the music of where the player stands: its region's, else its map's, else <see cref="MusicType.NoMusic" />.
    /// </summary>
    MusicType MusicOf(MobileEntity player);

    /// <summary>
    ///     Plays <paramref name="music" /> to the player until its next region change brings another track.
    /// </summary>
    void Play(MobileEntity player, MusicType music);
}
