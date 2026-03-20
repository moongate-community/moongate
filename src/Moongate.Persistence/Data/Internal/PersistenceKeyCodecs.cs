using MemoryPack;
using Moongate.UO.Data.Ids;

namespace Moongate.Persistence.Data.Internal;

internal static class PersistenceKeyCodecs
{
    public static byte[] SerializeSerial(Serial value) => MemoryPackSerializer.Serialize((uint)value);

    public static Serial DeserializeSerial(byte[] payload) => (Serial)MemoryPackSerializer.Deserialize<uint>(payload);
}
