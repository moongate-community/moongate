using MessagePack;
using Moongate.UO.Data.Ids;

namespace Moongate.Persistence.Data.Internal;

internal static class PersistenceKeyCodecs
{
    public static byte[] SerializeSerial(Serial value) => MessagePackSerializer.Serialize((uint)value);

    public static Serial DeserializeSerial(byte[] payload) => (Serial)MessagePackSerializer.Deserialize<uint>(payload);
}
