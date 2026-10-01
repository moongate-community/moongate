using ConsoleAppFramework;
using Moongate.Ultima.Types;
using Moongate.UoxItemConverter.Internal;

var app = ConsoleApp.Create();
app.Add("", Cli.Run);
app.Add("modernuo-spawns", Cli.ModernUoSpawns);
app.Run(args);

internal static class Cli
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
    public static int Run(
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
}
