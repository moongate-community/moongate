using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Brings a character into the world on a session: after the client chose it, or right after it was created.
/// </summary>
public interface ICharacterEnterWorldService
{
    /// <summary>
    ///     Tells whether the session may bring a character into the world: it plays none yet and no other session of
    ///     its account has one in the world (as ModernUO: one character per account at a time). Game loop only.
    /// </summary>
    bool CanEnter(GameSession session);

    /// <summary>
    ///     Marks the character in the world, sends the enter-world sequence and the message of the day, and publishes
    ///     <see cref="Data.Events.CharacterEnteredWorldEvent" />. A session that fails <see cref="CanEnter" /> gets a
    ///     popup and is disconnected.
    /// </summary>
    Task EnterAsync(PacketContext context, Serial accountId, CharacterForPlay play, CancellationToken cancellationToken);
}
