namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Which UOX3 script an item runs, read from its js folder: <c>jse_fileassociations.scp</c> numbers the scripts
///     ([SCRIPT_LIST], <c>500=item/lights.js</c>) and <c>jse_objectassociations.scp</c> gives graphics a script
///     ([ENVOKE], <c>0x0a28=500</c>); a block's own <c>script=</c> wins. Only the scripts Moongate has a Lua script for
///     become a <c>script_id</c>.
/// </summary>
internal sealed class UoxScriptAssociations
{
    private const string FileAssociations = "jse_fileassociations.scp";
    private const string ObjectAssociations = "jse_objectassociations.scp";

    // UOX3 script → the Moongate item script doing the same.
    private static readonly Dictionary<string, string> ScriptIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["item/lights.js"] = "light"
    };

    private readonly Dictionary<int, string> _files;
    private readonly Dictionary<int, int> _byGraphic;

    private UoxScriptAssociations(Dictionary<int, string> files, Dictionary<int, int> byGraphic)
    {
        _files = files;
        _byGraphic = byGraphic;
    }

    /// <summary>
    ///     Reads the two association files of <paramref name="directory" />.
    /// </summary>
    /// <exception cref="FileNotFoundException">One of them is missing.</exception>
    public static UoxScriptAssociations Load(string directory)
    {
        var files = new Dictionary<int, string>();
        var byGraphic = new Dictionary<int, int>();

        foreach (var (section, key, value) in ReadEntries(Path.Combine(directory, FileAssociations)))
        {
            if (section == "SCRIPT_LIST" && UoxNumber.TryParse(key, out var number))
            {
                files[number] = value.Replace('\\', '/');
            }
        }

        foreach (var (section, key, value) in ReadEntries(Path.Combine(directory, ObjectAssociations)))
        {
            if (section == "ENVOKE" && UoxNumber.TryParse(key, out var graphic) && UoxNumber.TryParse(value, out var number))
            {
                byGraphic[graphic] = number;
            }
        }

        return new(files, byGraphic);
    }

    /// <summary>
    ///     Gets the Moongate script of an item: its <c>script=</c> or its graphic's script, when Moongate has one.
    /// </summary>
    public string? ScriptIdFor(DfnBlock block, int graphic)
    {
        int number;

        if (block.Fields.TryGetValue("script", out var text) && UoxNumber.TryParse(text, out var own))
        {
            number = own;
        }
        else if (!_byGraphic.TryGetValue(graphic, out number))
        {
            return null;
        }

        return _files.TryGetValue(number, out var file) && ScriptIds.TryGetValue(file, out var scriptId) ? scriptId : null;
    }

    private static IEnumerable<(string Section, string Key, string Value)> ReadEntries(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{Path.GetFileName(path)} is missing from the scripts source.", path);
        }

        var section = string.Empty;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
            }
            else if (!line.StartsWith("//", StringComparison.Ordinal) && line.IndexOf('=') is > 0 and var equals)
            {
                yield return (section, line[..equals].Trim(), line[(equals + 1)..].Trim());
            }
        }
    }
}
