using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Fills every spawn region to its max at the next spawn check, about 10 seconds later: <c>initial_spawn</c>. The
///     spawn messages to the staff and the server log then show the progress.
/// </summary>
public sealed class InitialSpawnCommand : ICommandExecutor
{
    private readonly ISpawnRegionService _spawns;
    private readonly ILocalizationService? _localization;

    public InitialSpawnCommand(ISpawnRegionService spawns, ILocalizationService? localization = null)
    {
        _spawns = spawns;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "initial_spawn"));

            return;
        }

        var (regions, missing) = await _spawns.FillAllAsync();
        context.Print(
            _localization.Text(
                CommandMessages.InitialSpawnStarted,
                "Filling {0} spawn regions: {1} NPCs to spawn. The spawn messages show the progress.",
                regions,
                missing
            )
        );
    }
}
