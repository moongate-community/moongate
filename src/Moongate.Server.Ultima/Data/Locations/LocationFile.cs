namespace Moongate.Server.Ultima.Data.Locations;

/// <summary>
///     The root of <c>data/locations.toml</c>: one <c>[[location]]</c> per named place.
/// </summary>
public class LocationFile
{
    public List<NamedLocation> Location { get; set; } = [];
}
