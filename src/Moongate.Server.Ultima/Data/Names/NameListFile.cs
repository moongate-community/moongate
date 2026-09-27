namespace Moongate.Server.Ultima.Data.Names;

/// <summary>
///     <c>data/names.toml</c>: a <c>[[names]]</c> array of <see cref="NameList" />.
/// </summary>
public class NameListFile
{
    /// <summary>
    ///     The lists in the file.
    /// </summary>
    public List<NameList> Names { get; set; } = [];
}
