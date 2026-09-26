namespace Moongate.Server.Ultima.Data.Professions;

/// <summary>
///     The root of <c>professions.toml</c>: an array of tables under <c>profession</c>. The property name must match
///     the table name, otherwise the file reads as an empty list without any error.
/// </summary>
internal sealed class ProfessionContentFile
{
    public List<ProfessionContent> Profession { get; set; } = [];
}
