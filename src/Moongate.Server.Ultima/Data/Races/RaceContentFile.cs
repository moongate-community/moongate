namespace Moongate.Server.Ultima.Data.Races;

/// <summary>
///     The root of <c>races.toml</c>: an array of tables under <c>race</c>. The property name must match the table
///     name, otherwise the file reads as an empty list without any error.
/// </summary>
internal sealed class RaceContentFile
{
    public List<RaceContent> Race { get; set; } = [];
}
