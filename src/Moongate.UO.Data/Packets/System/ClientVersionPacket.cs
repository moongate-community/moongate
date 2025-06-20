using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.System;

public class ClientVersionPacket : BaseUoPacket
{
    public ClientVersionPacket() : base(0xBD)
    {
    }

    public string Version { get; set; }

    protected override bool Read(SpanReader reader)
    {
        reader.ReadByte();
        reader.ReadInt16();

        Version = reader.ReadAscii(reader.Remaining);
        return true;
    }


    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)(3));

        return writer.ToArray();
    }

    public override string ToString()
    {
        return $"{nameof(ClientVersionPacket)}: Version={Version}";
    }

}
