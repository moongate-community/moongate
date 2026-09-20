using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Sample.Plugin.Data.Persistence;

namespace Moongate.Sample.Plugin.Persistence;

/// <summary>Declares the sample's realm schema without connecting or changing the database during registration.</summary>
public sealed class GreeterPersistenceModule : IPersistenceModule
{
    /// <inheritdoc />
    public string Id => "com.github.moongate-community.moongate.plugins.greeter";
    /// <inheritdoc />
    public string Schema => "sample_greeter";
    /// <inheritdoc />
    public PersistenceDatabaseTarget DatabaseTarget => PersistenceDatabaseTarget.Realm;
    /// <inheritdoc />
    public IReadOnlyCollection<Type> EntityTypes => [typeof(GreetingNote)];
}
