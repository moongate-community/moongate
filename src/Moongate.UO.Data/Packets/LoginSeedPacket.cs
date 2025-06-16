using System.Net;
using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Version;

namespace Moongate.UO.Data.Packets;

public class LoginSeedPacket : BaseUoPacket
{

    public int Seed { get; set; }

    public ClientVersion ClientVersion { get; set; }

    public LoginSeedPacket() : base(0xEF)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        Seed = reader.ReadInt32();
        ClientVersion = new ClientVersion(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        return true;
    }
}
