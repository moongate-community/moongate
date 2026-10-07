using Moongate.Persistence.Internal;

namespace Moongate.Persistence.Interfaces.Internal;

/// <summary>
///     Captures one registered entity source for a later transactional write.
/// </summary>
internal interface IPersistenceEntityRegistration
{
    /// <summary>
    ///     Gets the registered entity type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    ///     Copies and validates the source on its owner loop, returning deferred database work.
    /// </summary>
    Func<PersistenceTransaction, CancellationToken, Task> Capture(out int entityCount);

    /// <summary>
    ///     Gets how many rows the last write upserted: the captured entities whose snapshot changed since the last
    ///     committed save.
    /// </summary>
    int Written { get; }

    /// <summary>
    ///     Tells the registration that the captured save was committed to the database.
    /// </summary>
    void Committed();
}
