using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Prints the spawn regions where the game master stands, <c>spawns</c>: their live NPCs against their max and the
///     minutes to their next spawn.
/// </summary>
public sealed class SpawnsCommand : ICommandExecutor
{
    private readonly ISpawnRegionService _spawns;
    private readonly IMobileService _mobiles;
    private readonly ILocalizationService? _localization;

    public SpawnsCommand(ISpawnRegionService spawns, IMobileService mobiles, ILocalizationService? localization = null)
    {
        _spawns = spawns;
        _mobiles = mobiles;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            context.PrintError("spawns works in game only.");

            return;
        }

        var here = await _spawns.RegionsAtAsync(character.Map, character.Location.X, character.Location.Y);

        if (here.Count == 0)
        {
            context.Print(_localization.Text(CommandMessages.NoSpawnRegionHere, "No spawn region here."));

            return;
        }

        foreach (var region in here)
        {
            context.Print(
                _localization.Text(
                    region.Retrying ? CommandMessages.SpawnRegionRetrying : CommandMessages.SpawnRegionHere,
                    region.Retrying
                        ? "{0} ({1}): {2}/{3} NPCs, no spot found, retrying in {4} min."
                        : "{0} ({1}): {2}/{3} NPCs, next spawn in {4} min.",
                    region.Name ?? region.Id,
                    region.Id,
                    region.Live,
                    region.Max,
                    (int)Math.Ceiling(region.NextSpawnIn.TotalMinutes)
                )
            );
        }
    }
}
