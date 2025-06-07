namespace Moongate.Core.Server.Interfaces.Packets;

public interface IUoNetworkPacket
{
    byte OpCode { get; }

    bool Parse(ReadOnlyMemory<byte> data);

    ReadOnlyMemory<byte> Write();
}
