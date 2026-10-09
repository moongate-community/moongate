using DryIoc;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the <c>[ultima]</c> section of <c>config/moongate.toml</c> and each of its sub-tables.
/// </summary>
public static class UltimaConfigContainerExtensions
{
    /// <summary>
    ///     Adds <see cref="UltimaConfig" /> with <c>AddConfig</c> and registers its sub-sections, so services keep
    ///     receiving <see cref="WorldConfig" />, <see cref="CharactersConfig" /> and the others.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     A value is not valid.
    /// </exception>
    public static UltimaConfig AddUltimaConfig(this Container container)
    {
        ArgumentNullException.ThrowIfNull(container);

        var ultima = container.AddConfig<UltimaConfig>("ultima");
        container.RegisterInstance(ultima.Localization);
        container.RegisterInstance(ultima.LineOfSight);
        container.RegisterInstance(ultima.World);
        container.RegisterInstance(ultima.Items);
        container.RegisterInstance(ultima.StartingItems);
        container.RegisterInstance(ultima.Characters);
        container.RegisterInstance(ultima.Npcs);
        container.RegisterInstance(ultima.Regeneration);
        container.RegisterInstance(ultima.Crime);
        container.RegisterInstance(ultima.Murder);
        container.RegisterInstance(ultima.Skills);
        container.RegisterInstance(ultima.Combat);
        container.RegisterInstance(ultima.Spawns);
        container.RegisterInstance(ultima.Jail);
        container.RegisterInstance(ultima.Help);
        container.RegisterInstance(ultima.BulletinBoards);
        container.RegisterInstance(ultima.Bank);
        container.RegisterInstance(ultima.Stable);
        container.RegisterInstance(ultima.Pets);

        return ultima;
    }
}
