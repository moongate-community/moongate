using ConsoleAppFramework;
using Moongate.UoxItemConverter.Internal;

ConsoleApp.Run(args, Cli.Run);

internal static class Cli
{
    /// <summary>
    ///     Converts UOX3 item and loot .dfn definitions into Moongate ItemTemplate/LootTemplate TOML.
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
    public static int Run(string source, string destination, string? lootDestination = null)
    {
        return UoxItemConverterCommand.Run(source, destination, lootDestination, Console.Out, Console.Error);
    }
}
