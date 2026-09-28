using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces.Motd;

/// <summary>Sends the configured private welcome message after character admission.</summary>
public interface IMotdService
{
    ValueTask SendAsync(PacketContext context, MobileEntity character, CancellationToken cancellationToken);
}
