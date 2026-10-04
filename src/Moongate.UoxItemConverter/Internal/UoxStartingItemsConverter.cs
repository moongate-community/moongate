using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.StartingItems;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts UOX3's <c>newbie/newbie.dfn</c> into <c>starting_items.toml</c>, against the item ids of the item pass.
/// </summary>
internal static class UoxStartingItemsConverter
{
    private const string Header = """
                                  # ==============================================================================
                                  # Moongate - starting_items.toml
                                  #
                                  # What it is for:
                                  #   The items a new character gets: every common set, plus every set whose
                                  #   filters it matches. The starting gold, the bread and the water are items
                                  #   of the common set.
                                  #
                                  # Fields of a [[set]]:
                                  #   common  true gives the set to every character
                                  #   skill   given to characters starting with this skill among their best ones
                                  #   race    human, elf or gargoyle; unset is every race
                                  #   gender  male or female; unset is both
                                  #
                                  # Fields of a [[set.items]]:
                                  #   items   item template ids; one is picked at random
                                  #   amount  how many, as dice; unset is 1
                                  #   hue     the hue to give the item; unset keeps its own
                                  #   equip   true puts it on the character, false in the backpack
                                  #   newbie  whether it stays on death; unset is the server's default
                                  # ==============================================================================


                                  """;

    public static int Run(string mobileSource, string destination, ItemIndex items, TextWriter output, TextWriter error)
    {
        var source = Path.Combine(mobileSource, "newbie", "newbie.dfn");

        if (!File.Exists(source))
        {
            error.WriteLine($"Starting items source does not exist: {source}");

            return 2;
        }

        var report = new ConversionReport();
        var sets = StartingItemsBuilder.Build(DfnParser.Parse(File.ReadAllLines(source)), items, report);

        ConverterOutput.WriteToml(destination, Header, new StartingItemsFile { Set = sets });
        output.WriteLine($"Converted {sets.Count} starting item set(s) to {destination}.");

        ConverterOutput.WriteReport(output, report);

        // Read back as the server will, and check every item exists.
        var itemIds = items.ItemIds;
        var written = TomlUtils.DeserializeFromFile<StartingItemsFile>(destination)?.Set ?? [];
        var errors = written.SelectMany(set => set.Items)
                            .SelectMany(entry => entry.Items)
                            .Where(item => !itemIds.Contains(item))
                            .Select(item => $"starting item '{item}' does not exist")
                            .ToList();

        if (ConverterOutput.ReportErrors(error, errors, "starting items") != 0)
        {
            return 1;
        }

        output.WriteLine($"Verified {written.Count} starting item set(s) read back from disk: every item resolves.");

        return 0;
    }
}
