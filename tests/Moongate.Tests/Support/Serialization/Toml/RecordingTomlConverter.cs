using Tomlyn.Serialization;

namespace Moongate.Tests.Support.Serialization.Toml;

/// <summary>
///     A no-op <see cref="TomlConverter{T}" /> for a type Tomlyn has no built-in converter for.
/// </summary>
public sealed class RecordingTomlConverter : TomlConverter<RecordingTomlConverter.Marker>
{
    /// <summary>
    ///     A type real enough for the registry tests, and nothing else.
    /// </summary>
    public readonly struct Marker;

    public override Marker Read(TomlReader reader)
    {
        reader.Skip();

        return default;
    }

    public override void Write(TomlWriter writer, Marker value)
    {
        writer.WriteBooleanValue(true);
    }
}
