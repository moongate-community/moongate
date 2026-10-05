using Moongate.Ctl.Data.Internal.Completion;
using Moongate.Ctl.Types.Completion;

namespace Moongate.Ctl.Internal;

/// <summary>
///     Every mgctl command with its options, as the completion scripts offer them. Keep it in step with the commands
///     Program.cs registers: a test compares it with what <c>--help</c> prints.
/// </summary>
internal static class CompletionCatalog
{
    private static readonly CompletionOption[] MigrateOptions =
    [
        new("--target", "The database, auth or world", CompletionValueType.Choice, "auth", "world"),
        new("--root-directory", "Root directory holding config/moongate.toml", CompletionValueType.Directory),
        new("--migrations-directory", "Core migrations directory", CompletionValueType.Directory),
        new("--plugins-directory", "Plugins directory also scanned for migrations", CompletionValueType.Directory)
    ];

    public static string[] Shells { get; } = ["bash", "zsh", "fish"];

    public static IReadOnlyList<CompletionCommand> Commands { get; } =
    [
        new(
            "init",
            "Prepare a server root",
            [
                new("--generate-admin-certificate", "Create a TLS certificate and enable the administration API", CompletionValueType.None),
                new("--admin-certificate-hosts", "Additional DNS names or IP addresses, comma separated", CompletionValueType.Text),
                new("--no-header", "Leave out the banner", CompletionValueType.None)
            ],
            new("root-directory", "Root directory to initialize", CompletionValueType.Directory)
        ),
        new("migrate status", "List the pending migrations of a database", MigrateOptions),
        new("migrate apply", "Apply the pending migrations of a database", MigrateOptions),
        new(
            "convert uox",
            "Convert UOX3 .dfn definitions into TOML",
            [
                new("--source", "A .dfn file or a directory of them", CompletionValueType.File),
                new("--destination", "Directory for the item templates", CompletionValueType.Directory),
                new("--loot-destination", "Directory for the loot templates", CompletionValueType.Directory),
                new("--mobile-source", "The dfndata folder of UOX3", CompletionValueType.Directory),
                new("--mobile-destination", "Directory for the mobile templates", CompletionValueType.Directory),
                new("--names-destination", "File for the name lists", CompletionValueType.File),
                new("--starting-items-destination", "File for the starting items", CompletionValueType.File),
                new("--scripts-source", "The js folder of UOX3", CompletionValueType.Directory),
                new("--npc-lists-destination", "Directory for the NPC lists", CompletionValueType.Directory),
                new("--spawns-destination", "Directory for the spawn regions", CompletionValueType.Directory)
            ]
        ),
        new(
            "convert modernuo-spawns",
            "Convert the spawners of ModernUO into spawn regions",
            [
                new("--source", "The Distribution/Data/Spawns folder of ModernUO", CompletionValueType.Directory),
                new("--maps", "The maps to convert, comma separated", CompletionValueType.Text),
                new("--mobiles", "The mobile templates folder", CompletionValueType.Directory),
                new("--destination", "The spawns folder", CompletionValueType.Directory)
            ]
        ),
        new(
            "convert modernuo-signs",
            "Convert the signs of ModernUO into decoration files",
            [
                new("--source", "The signs.cfg file of ModernUO", CompletionValueType.File),
                new("--destination", "The decorations folder", CompletionValueType.Directory)
            ]
        ),
        new(
            "convert modernuo-teleporters",
            "Convert the teleporters of ModernUO into decoration files",
            [
                new("--source", "The teleporters.json file of ModernUO", CompletionValueType.File),
                new("--destination", "The decorations folder", CompletionValueType.Directory)
            ]
        ),
        new(
            "convert modernuo-locations",
            "Convert the named places of ModernUO into locations.toml",
            [
                new("--source", "The Distribution/Data/Locations folder of ModernUO", CompletionValueType.Directory),
                new("--destination", "The locations.toml file to write", CompletionValueType.File)
            ]
        ),
        new(
            "convert modernuo-chests",
            "Convert the treasure chests of ModernUO's spawners into spawn regions",
            [
                new("--source", "The Distribution/Data/Spawns folder of ModernUO", CompletionValueType.Directory),
                new("--destination", "The spawns folder", CompletionValueType.Directory)
            ]
        ),
        new(
            "completion",
            "Print the completion script of a shell",
            [],
            new("shell", "The shell", CompletionValueType.Choice, Shells)
        )
    ];
}
