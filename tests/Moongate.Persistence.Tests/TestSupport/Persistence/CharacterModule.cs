using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

public sealed class CharacterModule : IPersistenceModule
{
    public string Id => "characters";
    public string Schema => "plugin_characters";
    public PersistenceDatabaseTarget DatabaseTarget => PersistenceDatabaseTarget.Realm;
    public IReadOnlyCollection<Type> EntityTypes => [typeof(CharacterEntity)];
}
