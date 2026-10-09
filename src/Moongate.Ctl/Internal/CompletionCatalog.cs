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
                new(
                    "--generate-admin-certificate",
                    "Create a TLS certificate and enable the administration API",
                    CompletionValueType.None
                ),
                new(
                    "--admin-certificate-hosts",
                    "Additional DNS names or IP addresses, comma separated",
                    CompletionValueType.Text
                ),
                new("--no-header", "Leave out the banner", CompletionValueType.None)
            ],
            new("root-directory", "Root directory to initialize", CompletionValueType.Directory)
        ),
        new("migrate status", "List the pending migrations of a database", MigrateOptions),
        new("migrate apply", "Apply the pending migrations of a database", MigrateOptions),
        new(
            "completion",
            "Print the completion script of a shell",
            [],
            new("shell", "The shell", CompletionValueType.Choice, Shells)
        )
    ];
}
