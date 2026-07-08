namespace Moongate.UO.Data.Names;

/// <summary>A named pool of candidate names for a given kind of mobile (e.g. "orc", "tokuno male").</summary>
public sealed class NameList
{
    public string Type { get; set; } = string.Empty;
    public List<string> Names { get; set; } = [];
}
