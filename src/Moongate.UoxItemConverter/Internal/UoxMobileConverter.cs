using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Names;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts UOX3 NPCs and name lists, after the item pass, against the item and loot ids it computed.
/// </summary>
internal static class UoxMobileConverter
{
    private const string NamesHeader = """
                                       # ==============================================================================
                                       # Moongate - names.toml
                                       #
                                       # What it is for:
                                       #   The lists random NPC names are drawn from. A mobile template names a list with
                                       #   name_list (for example "male", or "{gender}" for the list of the gender the
                                       #   mobile gets); the server picks one name from it at random.
                                       #
                                       # Fields:
                                       #   id      the list id, unique ignoring case
                                       #   names   the names; none may be empty
                                       #
                                       # Source: UOX3 dfndata/npc/namelists.dfn, converted by mg-uoxconv.
                                       # ==============================================================================

                                       """;

    public static int Run(
        string mobileSource,
        string mobileDestination,
        string namesDestination,
        ItemIndex items,
        TextWriter output,
        TextWriter error
    )
    {
        var dictionary = UoxDictionary.Load(Path.Combine(mobileSource, "..", "dictionaries", "dictionary.ENG"));
        var nameListsPath = Path.Combine(mobileSource, "npc", "namelists.dfn");
        var nameLists = File.Exists(nameListsPath)
            ? NameListsBuilder.Build(DfnParser.Parse(File.ReadAllLines(nameListsPath)), dictionary)
            : [];

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(namesDestination))!);
        File.WriteAllText(namesDestination, NamesHeader + TomlUtils.Serialize(new NameListFile { Names = nameLists }));
        output.WriteLine($"Converted {nameLists.Count} name list(s) to {namesDestination}.");

        return 0;
    }
}
