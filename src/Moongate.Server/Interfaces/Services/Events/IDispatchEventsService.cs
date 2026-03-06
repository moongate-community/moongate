using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Interfaces.Services.Events;

/// <summary>
/// Centralizes outbound gameplay packet dispatch for mobile updates and speech.
/// </summary>
public interface IDispatchEventsService
{
    Task<bool> DispatchEffectToPlayerAsync(
        Serial characterId,
        Point3D location,
        ushort itemId,
        byte speed = 10,
        byte duration = 10,
        int hue = 0,
        int renderMode = 0,
        ushort effect = 0,
        ushort explodeEffect = 0,
        ushort explodeSound = 0,
        byte layer = 0xFF,
        ushort unknown3 = 0,
        CancellationToken cancellationToken = default
    );

    Task<int> DispatchMobileEffectAsync(
        int mapId,
        Point3D location,
        ushort itemId,
        byte speed = 10,
        byte duration = 10,
        int hue = 0,
        int renderMode = 0,
        ushort effect = 0,
        ushort explodeEffect = 0,
        ushort explodeSound = 0,
        byte layer = 0xFF,
        ushort unknown3 = 0,
        int? range = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Dispatches a mobile-triggered sound effect to players in range.
    /// </summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="location">Sound source location.</param>
    /// <param name="soundModel">Sound id.</param>
    /// <param name="mode">Playback mode.</param>
    /// <param name="unknown3">Packet unknown field.</param>
    /// <param name="range">Optional explicit range; when null service defaults are used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of recipients.</returns>
    Task<int> DispatchMobileSoundAsync(
        int mapId,
        Point3D location,
        ushort soundModel,
        byte mode = 0x01,
        ushort unknown3 = 0,
        int? range = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Dispatches mobile speech packets to players in range.
    /// </summary>
    /// <param name="speaker">Speaker mobile.</param>
    /// <param name="text">Speech text.</param>
    /// <param name="range">Tile range used to resolve recipients.</param>
    /// <param name="messageType">Speech message type.</param>
    /// <param name="hue">Speech hue.</param>
    /// <param name="font">Speech font.</param>
    /// <param name="language">Language code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of recipients that received the message.</returns>
    Task<int> DispatchMobileSpeechAsync(
        UOMobileEntity speaker,
        string text,
        int range,
        ChatMessageType messageType = ChatMessageType.Regular,
        short hue = SpeechHues.Default,
        short font = SpeechHues.DefaultFont,
        string language = "ENU",
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Dispatches a mobile visibility update to players in range.
    /// </summary>
    /// <param name="mobile">Mobile being updated.</param>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="range">Tile range used to resolve recipients.</param>
    /// <param name="isNew">
    /// True to send full incoming payload (0x78 + status + worn); false to send moving update (0x77).
    /// </param>
    /// <param name="stygianAbyss">Whether to apply stygian packet-flag semantics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of recipients that received packets.</returns>
    Task<int> DispatchMobileUpdateAsync(
        UOMobileEntity mobile,
        int mapId,
        int range,
        bool isNew,
        bool stygianAbyss = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Dispatches a sound effect packet to a single player session resolved by character id.
    /// </summary>
    /// <param name="characterId">Character serial.</param>
    /// <param name="location">Sound source location.</param>
    /// <param name="soundModel">Sound id.</param>
    /// <param name="mode">Playback mode.</param>
    /// <param name="unknown3">Packet unknown field.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a session was found and enqueued; otherwise <c>false</c>.</returns>
    Task<bool> DispatchSoundToPlayerAsync(
        Serial characterId,
        Point3D location,
        ushort soundModel,
        byte mode = 0x01,
        ushort unknown3 = 0,
        CancellationToken cancellationToken = default
    );
}
