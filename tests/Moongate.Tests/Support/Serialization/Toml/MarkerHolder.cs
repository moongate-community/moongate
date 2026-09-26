namespace Moongate.Tests.Support.Serialization.Toml;

/// <summary>
///     Wraps a <see cref="RecordingTomlConverter.Marker" /> in a named property, since a TOML
///     document is always a table and cannot serialize a bare scalar at its root.
/// </summary>
public sealed class MarkerHolder
{
    public RecordingTomlConverter.Marker Value { get; set; }
}
