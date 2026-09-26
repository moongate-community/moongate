using Tomlyn.Serialization;

namespace Moongate.Tests.Support.Serialization.Toml;

/// <summary>
///     A no-op converter whose type is distinct for every <typeparamref name="TMarker" />, so registry tests can add and
///     remove converters that no other test serializes with.
/// </summary>
public sealed class RegistryTomlConverter<TMarker> : TomlConverter<TMarker> where TMarker : struct
{
    public override TMarker Read(TomlReader reader)
    {
        reader.Skip();

        return default;
    }

    public override void Write(TomlWriter writer, TMarker value)
    {
        writer.WriteBooleanValue(true);
    }
}
