using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.System;

public class ClientVersionPacket : BaseUoPacket
{
    public ClientVersionPacket() : base(0xBD) { }

    public string Version { get; set; }

    public override string ToString()
        => $"{nameof(ClientVersionPacket)}: Version={Version}";

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)3);

        return writer.ToArray();
    }

    protected override bool Read(SpanReader reader)
    {
        reader.ReadByte();
        reader.ReadInt16();

        Version = reader.ReadAscii(reader.Remaining);

        return true;
    }
}
