using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces.Persistence;

namespace Moongate.Persistence.Services.Persistence;

internal sealed class GenericRepository<TEntity, TKey> : BaseRepository<TEntity, TKey>
    where TKey : notnull
{
    public GenericRepository(
        PersistenceStateStore stateStore,
        IJournalService journalService,
        IPersistenceEntityDescriptor<TEntity, TKey> descriptor
    )
        : base(stateStore, journalService, descriptor) { }
}
