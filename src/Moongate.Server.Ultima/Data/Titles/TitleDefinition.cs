namespace Moongate.Server.Ultima.Data.Titles;

/// <summary>
///     A raw TOML title row. Nullable required fields distinguish missing keys from zero and empty values.
/// </summary>
public sealed class TitleDefinition
{
    public int? Fame { get; set; }

    public int? Karma { get; set; }

    public string? Title { get; set; }

    public string? FemaleTitle { get; set; }
}
