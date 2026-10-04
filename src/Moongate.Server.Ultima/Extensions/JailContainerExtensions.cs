using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the jail sentences with persistence so the world save writes the live ones from
///     <see cref="IJailService" />.
/// </summary>
public static class JailContainerExtensions
{
    /// <summary>
    ///     Registers <see cref="JailSentenceEntity" /> in the world database with the live sentences as the save source,
    ///     each saved through <see cref="JailSentenceEntity.Snapshot" />, and the ended sentences as its deletions.
    /// </summary>
    public static Container AddLiveJailSentences(this Container container)
    {
        return container.AddPersistenceWorld<JailSentenceEntity>(
            () => container.Resolve<IJailService>().Sentences,
            sentence => sentence.Snapshot(),
            new LazyDeletionSource(() => container.Resolve<IJailService>())
        );
    }
}
