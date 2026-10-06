using Moongate.Ultima.Types;
using Moongate.UoxItemConverter.Internal;

namespace Moongate.Ctl.Internal;

/// <summary>
///     The <c>mgctl convert</c> commands: UOX3 and ModernUO content into Moongate TOML.
/// </summary>
internal static class ConvertCommands
{
    /// <summary>
    ///     Converts UOX3 .dfn definitions into Moongate TOML: item templates and loot tables, and, with the mobile
    ///     options, mobile templates, name lists and starting items.
    /// </summary>
    /// <param name="source">
    ///     A single .dfn file, or a directory scanned recursively for *.dfn files.
    /// </param>
    /// <param name="destination">
    ///     Directory to write the converted ItemTemplate .toml files under.
    /// </param>
    /// <param name="lootDestination">
    ///     Directory to write the converted LootTemplate .toml files under. Omit it to leave every
    ///     [LOOTLIST ...] block unconverted.
    /// </param>
    /// <param name="mobileSource">
    ///     UOX3's dfndata folder, holding npc/, colors/, creatures/ and newbie/. Goes with mobileDestination and
    ///     namesDestination.
    /// </param>
    /// <param name="mobileDestination">
    ///     Directory to write the converted mobile templates under, one file per source file.
    /// </param>
    /// <param name="namesDestination">
    ///     File to write the name lists of npc/namelists.dfn to (names.toml).
    /// </param>
    /// <param name="startingItemsDestination">
    ///     File to write the starting items of newbie/newbie.dfn to (starting_items.toml). Needs mobileSource.
    /// </param>
    /// <param name="npcListsDestination">
    ///     Directory to write UOX3's NPC lists (npc/**/[NPCLIST ...]) to, as templates/npc_lists. Needs mobileSource and
    ///     spawnsDestination.
    /// </param>
    /// <param name="spawnsDestination">
    ///     Directory to write UOX3's spawn regions (spawn/**/[REGIONSPAWN ...]) to, one folder per map, as
    ///     templates/spawns. Needs mobileSource and npcListsDestination.
    /// </param>
    /// <param name="scriptsSource">
    ///     UOX3's js folder, holding jse_fileassociations.scp and jse_objectassociations.scp: the items whose UOX3 script
    ///     has a Moongate Lua script (item/lights.js is light) get its script_id.
    /// </param>
    public static int Uox(
        string source,
        string destination,
        string? lootDestination = null,
        string? mobileSource = null,
        string? mobileDestination = null,
        string? namesDestination = null,
        string? startingItemsDestination = null,
        string? scriptsSource = null,
        string? npcListsDestination = null,
        string? spawnsDestination = null
    )
    {
        return UoxItemConverterCommand.Run(
            source,
            destination,
            lootDestination,
            Console.Out,
            Console.Error,
            mobileSource,
            mobileDestination,
            namesDestination,
            startingItemsDestination,
            scriptsSource,
            npcListsDestination,
            spawnsDestination
        );
    }

    /// <summary>
    ///     Converts ModernUO's spawners of the chosen maps (the shared and post-uoml eras) into Moongate spawn regions,
    ///     for the maps UOX3 has no spawns for, such as Malas, Tokuno and TerMur.
    /// </summary>
    /// <param name="source">
    ///     ModernUO's Distribution/Data/Spawns folder.
    /// </param>
    /// <param name="maps">
    ///     The maps to convert, comma separated, such as malas,tokuno,termur.
    /// </param>
    /// <param name="mobiles">
    ///     The mobile templates folder (templates/mobiles): spawners naming no template there are skipped.
    /// </param>
    /// <param name="destination">
    ///     The spawns folder (templates/spawns); each map gets modernuo_*.toml files, replacing those of a previous run.
    /// </param>
    public static int ModernUoSpawns(string source, string maps, string mobiles, string destination)
    {
        var chosen = new List<MapType>();

        foreach (var name in maps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<MapType>(name, true, out var map) || !Enum.IsDefined(map))
            {
                Console.Error.WriteLine($"Unknown map: {name}");

                return 2;
            }

            chosen.Add(map);
        }

        UoxItemConverterCommand.RegisterTomlConverters();

        return ModernUoSpawnConverter.Run(
            Path.GetFullPath(source),
            chosen,
            Path.GetFullPath(mobiles),
            Path.GetFullPath(destination),
            Console.Out,
            Console.Error
        );
    }

    /// <summary>
    ///     Converts ModernUO's shop and world signs (signs.cfg) into decoration files, one signs.toml per folder of
    ///     templates/decorations, which .decorate places.
    /// </summary>
    /// <param name="source">
    ///     ModernUO's Distribution/Data/signs.cfg file.
    /// </param>
    /// <param name="destination">
    ///     The decorations folder (templates/decorations); each folder gets a signs.toml, replacing that of a previous run
    ///     (Trammel's, the signs of the old Haven, is written set aside as _signs.toml).
    /// </param>
    public static int ModernUoSigns(string source, string destination)
    {
        return ModernUoSignConverter.Run(Path.GetFullPath(source), Path.GetFullPath(destination), Console.Out, Console.Error);
    }

    /// <summary>
    ///     Converts ModernUO's world and dungeon teleporters (teleporters.json) into decoration files, one
    ///     teleporters.toml per map folder of templates/decorations, which .decorate places.
    /// </summary>
    /// <param name="source">
    ///     ModernUO's Distribution/Data/teleporters.json file.
    /// </param>
    /// <param name="destination">
    ///     The decorations folder (templates/decorations); each map folder gets a teleporters.toml, replacing that of a
    ///     previous run.
    /// </param>
    public static int ModernUoTeleporters(string source, string destination)
    {
        return ModernUoTeleporterConverter.Run(Path.GetFullPath(source), Path.GetFullPath(destination), Console.Out, Console.Error);
    }

    /// <summary>
    ///     Converts the named places of ModernUO's go gump (Data/Locations) into the data file locations.toml, which
    ///     .go lists and travels to.
    /// </summary>
    /// <param name="source">
    ///     ModernUO's Distribution/Data/Locations folder.
    /// </param>
    /// <param name="destination">
    ///     The file to write (data/locations.toml), replacing that of a previous run.
    /// </param>
    public static int ModernUoLocations(string source, string destination)
    {
        return ModernUoLocationConverter.Run(Path.GetFullPath(source), Path.GetFullPath(destination), Console.Out, Console.Error);
    }

    /// <summary>
    ///     Converts the treasure chests of ModernUO's spawners into spawn regions of items, one treasure_chests.toml per
    ///     map folder of templates/spawns.
    /// </summary>
    /// <param name="source">
    ///     ModernUO's Distribution/Data/Spawns folder.
    /// </param>
    /// <param name="destination">
    ///     The spawns folder (templates/spawns); each map folder gets a treasure_chests.toml, replacing that of a
    ///     previous run.
    /// </param>
    public static int ModernUoChests(string source, string destination)
    {
        return ModernUoChestConverter.Run(Path.GetFullPath(source), Path.GetFullPath(destination), Console.Out, Console.Error);
    }

    /// <summary>
    ///     Converts ModernUO's static BookContent definitions into readable document TOML without executing scripts.
    /// </summary>
    /// <param name="source">
    ///     The Projects/UOContent folder of ModernUO, or a folder containing static book C# definitions.
    /// </param>
    /// <param name="destination">
    ///     The book templates folder to receive one TOML file a book, named after its class; existing generated names are replaced.
    /// </param>
    public static int ModernUoBooks(string source, string destination)
    {
        return ModernUoBookConverter.Run(Path.GetFullPath(source), Path.GetFullPath(destination), Console.Out, Console.Error);
    }
}
