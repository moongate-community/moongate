using Moongate.Persistence.Interfaces;

namespace Moongate.Persistence.Data.Internal;

internal sealed class PersistenceModuleRegistration
{
    public IPersistenceModule Module { get; }

    public IReadOnlyList<Type> EntityTypes { get; }

    public PersistenceModuleRegistration(IPersistenceModule module, IReadOnlyList<Type> entityTypes)
    {
        Module = module;
        EntityTypes = entityTypes;
    }
}
