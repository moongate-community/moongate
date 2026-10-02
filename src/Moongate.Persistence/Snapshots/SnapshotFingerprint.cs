using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Moongate.Persistence.Snapshots;

/// <summary>
///     The fingerprint of a world-save snapshot: the first 128 bits of the SHA-256 of its JSON. Two snapshots with the
///     same settable property values share it, so the world save writes only the entities whose fingerprint changed.
/// </summary>
public static class SnapshotFingerprint
{
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { LeaveOutComputedProperties } }
    };

    /// <summary>
    ///     Gets the fingerprint of <paramref name="snapshot" />, by its runtime type.
    /// </summary>
    public static UInt128 Of(object snapshot)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(snapshot, snapshot.GetType(), Options);

        return BinaryPrimitives.ReadUInt128LittleEndian(SHA256.HashData(json));
    }

    // A class's read-only properties are computed from the others (and may throw on a partial state); a struct's are
    // its value, such as Serial.Value, and stay.
    private static void LeaveOutComputedProperties(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.IsValueType)
        {
            return;
        }

        for (var index = info.Properties.Count - 1; index >= 0; index--)
        {
            if (info.Properties[index].Set is null)
            {
                info.Properties.RemoveAt(index);
            }
        }
    }
}
