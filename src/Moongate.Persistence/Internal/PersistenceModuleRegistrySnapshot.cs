using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal sealed class PersistenceModuleRegistrySnapshot
{
    private readonly IReadOnlyDictionary<Type, IPersistenceModule> _entityOwners;

    public IReadOnlyList<PersistenceModuleRegistration> Modules { get; }

    public PersistenceModuleRegistrySnapshot(
        IReadOnlyList<PersistenceModuleRegistration> modules,
        IReadOnlyDictionary<Type, IPersistenceModule> entityOwners
    )
    {
        Modules = modules;
        _entityOwners = entityOwners;
    }

    public IReadOnlyList<PersistenceModuleRegistration> GetModules(PersistenceDatabaseTarget target)
    {
        return Modules.Where(module => module.Module.DatabaseTarget == target).ToArray();
    }

    public IPersistenceModule GetOwner(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return _entityOwners.TryGetValue(entityType, out var owner)
            ? owner
            : throw new InvalidOperationException($"Persistence entity '{entityType.FullName}' has no module owner.");
    }
}
