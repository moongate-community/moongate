using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Commands;

/// <summary>
///     Prints the version the server runs, <c>version</c>: its number and codename, whether it is a Debug or a
///     Release build, and when it was built.
/// </summary>
public sealed class VersionCommand : ICommandExecutor
{
    private readonly IAdminServerInfoProvider _info;
    private readonly ILocalizationService? _localization;

    public VersionCommand(IAdminServerInfoProvider info, ILocalizationService? localization = null)
    {
        _info = info;
        _localization = localization;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "version"));

            return Task.CompletedTask;
        }

        var info = _info.GetSnapshot();
        context.Print(
            _localization.Text(
                CommandMessages.VersionText,
                "Moongate {0} \"{1}\" ({2}), built {3}.",
                info.Version,
                info.Codename,
                VersionUtils.FormatBuildConfiguration(info.Configuration),
                VersionUtils.FormatBuildTime(info.BuiltAt)
            )
        );

        return Task.CompletedTask;
    }
}
