using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        // NaN and the infinities are rare prop values, but they must not stop a save.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
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

    // A class's getter-only properties are computed from the others (and may throw on a partial state); one with a
    // setter, even a private one, may be a saved column and stays. A struct's are its value, such as Serial.Value.
    private static void LeaveOutComputedProperties(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.IsValueType)
        {
            return;
        }

        for (var index = info.Properties.Count - 1; index >= 0; index--)
        {
            if (info.Properties[index].AttributeProvider is PropertyInfo { SetMethod: null })
            {
                info.Properties.RemoveAt(index);
            }
        }
    }
}
