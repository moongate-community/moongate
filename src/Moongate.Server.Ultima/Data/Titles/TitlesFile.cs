namespace Moongate.Server.Ultima.Data.Titles;

/// <summary>
///     The <c>[[titles]]</c> array in <c>data/titles.toml</c>.
/// </summary>
public sealed class TitlesFile
{
    public List<TitleDefinition>? Titles { get; set; }
}
