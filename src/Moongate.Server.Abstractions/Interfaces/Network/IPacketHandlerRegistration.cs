namespace Moongate.Server.Abstractions.Interfaces.Network;

/// <summary>
/// A packet handler that registers itself with the network service at startup. Lets the
/// generic <see cref="IPacketHandler{TPacket}" /> instances be discovered from DI as a set.
/// </summary>
public interface IPacketHandlerRegistration
{
    void Register(INetworkService network);
}
