using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Interfaces.Services.Events;

/// <summary>
/// Centralizes outbound gameplay packet dispatch for mobile updates and speech.
/// </summary>
public interface IDispatchEventsService
{
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
}
