using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces.Motd;

/// <summary>
///     Sends the configured private welcome message after character admission.
/// </summary>
public interface IMotdService
{
    /// <summary>
    ///     Sends the validated MOTD lines to the original connection for a character that entered the world.
    /// </summary>
    ValueTask SendAsync(PacketContext context, MobileEntity character, CancellationToken cancellationToken);
}
