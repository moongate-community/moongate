using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Gives every player the same light, as ModernUO's <c>GlobalLight</c>: <c>globallight &lt;0-31&gt;</c> sets it and
///     <c>globallight</c> goes back to the time of day. It is not saved.
/// </summary>
public sealed class GlobalLightCommand : ICommandExecutor
{
    private const int Darkest = 31;

    private readonly ILightService _light;
    private readonly ILocalizationService? _localization;

    public GlobalLightCommand(ILightService light, ILocalizationService? localization = null)
    {
        _localization = localization;
        _light = light;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length == 0)
        {
            await _light.SetOverrideAsync(null, context.CancellationToken);
            context.Print(_localization.Text(CommandMessages.GlobalLightCleared, "The global light follows the time of day again."));

            return;
        }

        if (context.Arguments.Length != 1 ||
            !int.TryParse(context.Arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var level) ||
            level > Darkest)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "globallight [0-31]"));

            return;
        }

        await _light.SetOverrideAsync(level, context.CancellationToken);
        context.Print(_localization.Text(CommandMessages.GlobalLightSet, "The global light is now {0}.", level));
    }
}
