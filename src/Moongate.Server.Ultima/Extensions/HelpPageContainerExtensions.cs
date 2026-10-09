using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the world persistence of the requests for a game master, which the world save writes from the live
///     <see cref="IHelpPageService" />.
/// </summary>
public static class HelpPageContainerExtensions
{
    /// <summary>
    ///     Adds the persistence module of the help pages.
    /// </summary>
    public static Container AddLiveHelpPages(this Container container)
    {
        return container.AddPersistenceWorld<HelpPageEntity>(
            () => container.Resolve<IHelpPageService>().Pages,
            page => page.Snapshot(),
            new LazyDeletionSource(() => container.Resolve<IHelpPageService>())
        );
    }
}
