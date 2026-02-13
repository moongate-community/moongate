using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Version;

namespace Moongate.UO.Data.Packets.Login;

public class LoginSeedPacket : BaseUoPacket
{
    public int Seed { get; set; }

    public ClientVersion ClientVersion { get; set; }

    public LoginSeedPacket() : base(0xEF) { }

    protected override bool Read(SpanReader reader)
    {
        Seed = reader.ReadInt32();
        ClientVersion = new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

        return true;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Seed);
        if (ClientVersion != null)
        {
            writer.Write(ClientVersion.Major);
            writer.Write(ClientVersion.Minor);
            writer.Write(ClientVersion.Revision);
            writer.Write(ClientVersion.Patch);
        }
        else
        {
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
        }

        return writer.ToArray();
    }
}
