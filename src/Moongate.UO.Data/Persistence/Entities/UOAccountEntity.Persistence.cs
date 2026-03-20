using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity(global::Moongate.UO.Data.Persistence.PersistenceCoreEntityTypeIds.Account, "account", 1, typeof(global::Moongate.UO.Data.Ids.Serial))]
public partial class UOAccountEntity;
