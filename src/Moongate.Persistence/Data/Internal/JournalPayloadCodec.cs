using MessagePack;
using Moongate.Persistence.Data.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Data.Internal;

/// <summary>
/// Encodes and decodes operation payloads used by journal entries.
/// </summary>
internal static class JournalPayloadCodec
{
    public static UOAccountEntity DecodeAccount(byte[] payload)
        => SnapshotMapper.ToAccountEntity(MessagePackSerializer.Deserialize<AccountSnapshot>(payload)!);

    public static UOItemEntity DecodeItem(byte[] payload)
        => SnapshotMapper.ToItemEntity(MessagePackSerializer.Deserialize<ItemSnapshot>(payload)!);

    public static UOMobileEntity DecodeMobile(byte[] payload)
        => SnapshotMapper.ToMobileEntity(MessagePackSerializer.Deserialize<MobileSnapshot>(payload)!);

    public static Serial DecodeSerial(byte[] payload)
        => (Serial)MessagePackSerializer.Deserialize<uint>(payload);

    public static byte[] EncodeAccount(UOAccountEntity account)
        => MessagePackSerializer.Serialize(SnapshotMapper.ToAccountSnapshot(account));

    public static byte[] EncodeItem(UOItemEntity item)
        => MessagePackSerializer.Serialize(SnapshotMapper.ToItemSnapshot(item));

    public static byte[] EncodeMobile(UOMobileEntity mobile)
        => MessagePackSerializer.Serialize(SnapshotMapper.ToMobileSnapshot(mobile));

    public static byte[] EncodeSerial(Serial id)
        => MessagePackSerializer.Serialize((uint)id);
}
