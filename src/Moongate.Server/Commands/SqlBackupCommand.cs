using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Commands;

/// <summary>
///     Makes a SQL backup now and lists the files it wrote: <c>sql_backup</c>.
/// </summary>
public sealed class SqlBackupCommand : ICommandExecutor
{
    private const string UsageText = "sql_backup";

    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    private readonly ISqlBackupService _backups;
    private readonly ILocalizationService? _localization;

    public SqlBackupCommand(ISqlBackupService backups, ILocalizationService? localization = null)
    {
        _backups = backups;
        _localization = localization;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        var result = await _backups.BackupAsync(context.CancellationToken).ConfigureAwait(false);

        if (result.AlreadyRunning)
        {
            context.PrintWarning(
                _localization.Text(CommandMessages.SqlBackupRunning, "A SQL backup is already running.")
            );

            return;
        }

        context.Print(_localization.Text(CommandMessages.SqlBackupStarted, "SQL backup started."));

        foreach (var file in result.Files)
        {
            context.Print(
                _localization.Text(
                        CommandMessages.SqlBackupFileWritten,
                        "{0} ({1})",
                        Path.GetFileName(file.Path),
                        FormatSize(file.Size)
                    )
            );
        }

        foreach (var failure in result.Failures)
        {
            context.PrintError(
                _localization.Text(
                        CommandMessages.SqlBackupFailed,
                        "SQL backup of {0} failed: {1}.",
                        failure.Database,
                        failure.Reason
                    )
            );
        }
    }

    /// <summary>
    ///     Formats a size in binary units with at most one decimal.
    /// </summary>
    internal static string FormatSize(long bytes)
    {
        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return size.ToString("0.#", CultureInfo.InvariantCulture) + " " + Units[unit];
    }
}
