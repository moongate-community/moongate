using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What the players around a mobile hear: its overhead speech and the sounds it makes. Called on the game loop.
/// </summary>
public interface ISpeechService
{
    /// <summary>
    ///     Sends <paramref name="text" /> as regular speech of <paramref name="speaker" /> (0xAE) to the players within 15
    ///     cells on its map.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int Say(MobileEntity speaker, string text);

    /// <summary>
    ///     Makes the mobile say a text of the client, by its cliloc number, to the players who hear it: each reads it
    ///     in the language of its client. <paramref name="arguments" /> fills the text's placeholders, tab separated.
    ///     <paramref name="affix" />, when not empty, is written by the client after the text. The number of players
    ///     reached.
    /// </summary>
    int SayCliloc(MobileEntity speaker, int cliloc, string arguments = "", string affix = "");

    /// <summary>
    ///     Plays <paramref name="sound" /> once where <paramref name="source" /> stands (0x54), for the players within 15
    ///     cells on its map.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int PlaySound(MobileEntity source, int sound);

    /// <summary>
    ///     Plays <paramref name="sound" /> once at <paramref name="location" /> on <paramref name="map" /> (0x54), for the
    ///     players within 15 cells, such as where an item lies.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int PlaySound(MapType map, Point3D location, int sound);

    /// <summary>
    ///     Sends <paramref name="text" /> as a system message (0xAE) to the player <paramref name="player" /> only, in
    ///     the lower left of its screen.
    /// </summary>
    /// <returns>Whether it was sent: false for an NPC or a player whose client is gone.</returns>
    bool Tell(MobileEntity player, string text, int? hue = null);

    /// <summary>
    ///     Sends a text of the client, by its number, as a system message (0xC1) to the player
    ///     <paramref name="player" /> only: the client shows it in its own language, with
    ///     <paramref name="arguments" /> in its <c>~1_NAME~</c> places, split by tabs.
    /// </summary>
    /// <returns>Whether it was sent: false for an NPC or a player whose client is gone.</returns>
    bool TellCliloc(MobileEntity player, int cliloc, string arguments = "", int? hue = null);
}
