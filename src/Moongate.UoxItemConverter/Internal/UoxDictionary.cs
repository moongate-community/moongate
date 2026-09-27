namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads a UOX3 <c>dictionary.ENG</c>: <c>id=text</c> lines, which numeric NPC names, titles and name list entries
///     refer to.
/// </summary>
internal static class UoxDictionary
{
    /// <summary>
    ///     Loads the texts by id; a missing file gives none.
    /// </summary>
    public static IReadOnlyDictionary<int, string> Load(string dictionaryPath)
    {
        var texts = new Dictionary<int, string>();

        if (!File.Exists(dictionaryPath))
        {
            return texts;
        }

        foreach (var line in File.ReadLines(dictionaryPath))
        {
            var separator = line.IndexOf('=');

            if (separator > 0 && int.TryParse(line[..separator].Trim(), out var id))
            {
                texts[id] = line[(separator + 1)..].Trim();
            }
        }

        return texts;
    }
}
