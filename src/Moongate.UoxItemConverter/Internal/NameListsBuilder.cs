using Moongate.Server.Ultima.Data.Names;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Turns UOX3 <c>[RANDOMNAME n]</c> blocks into <see cref="NameList" />s with readable ids.
/// </summary>
internal static class NameListsBuilder
{
    private const string HeaderPrefix = "RANDOMNAME ";

    // UOX3 numbers its lists; the ids name what uses them (list 12 is used by no NPC).
    private static readonly Dictionary<int, string> ListIds = new()
    {
        [1] = "male", [2] = "female", [3] = "orc", [4] = "lizardman", [5] = "daemon", [6] = "ratman",
        [7] = "balron", [8] = "bird", [9] = "ethereal_warrior", [10] = "centaur", [11] = "pixie", [12] = "list_12",
        [13] = "fire_gargoyle", [14] = "dark_father", [15] = "darknight_creeper", [16] = "impaler",
        [17] = "shadow_knight", [18] = "golem_controller", [19] = "savage", [20] = "ancient_lich"
    };

    /// <summary>
    ///     Gets the list id for UOX3's list number <paramref name="number" />.
    /// </summary>
    public static string ListId(int number)
    {
        return ListIds.TryGetValue(number, out var id) ? id : $"list_{number}";
    }

    /// <summary>
    ///     Builds one list per <c>[RANDOMNAME n]</c> block, in the order of the blocks. A number is a dictionary id,
    ///     else its <c>//</c> comment; duplicates are kept once.
    /// </summary>
    public static List<NameList> Build(IEnumerable<DfnBlock> blocks, IReadOnlyDictionary<int, string> dictionary)
    {
        var lists = new List<NameList>();

        foreach (var block in blocks)
        {
            if (!block.Header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(block.Header[HeaderPrefix.Length..].Trim(), out var number))
            {
                continue;
            }

            var names = new List<string>();

            for (var i = 0; i < block.Entries.Count; i++)
            {
                var entry = block.Entries[i].Trim();
                var name = int.TryParse(entry, out var textId)
                    ? dictionary.GetValueOrDefault(textId) ?? block.EntryComments[i]
                    : entry;

                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count > 0)
            {
                lists.Add(new() { Id = ListId(number), Names = names });
            }
        }

        return lists;
    }
}
