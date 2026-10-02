using Moongate.Core.Primitives;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Brings a character into the world on a session: after the client chose it, or right after it was created.
/// </summary>
public interface ICharacterEnterWorldService
{
    /// <summary>
    ///     Marks the character in the world, sends the enter-world sequence and the message of the day, and publishes
    ///     <see cref="Data.Events.CharacterEnteredWorldEvent" />. An account that already has a character in the world
    ///     gets a popup and is disconnected.
    /// </summary>
    Task EnterAsync(PacketContext context, Serial accountId, CharacterForPlay play, CancellationToken cancellationToken);
}
