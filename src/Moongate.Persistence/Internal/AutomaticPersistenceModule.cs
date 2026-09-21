using System.Security.Cryptography;
using System.Text;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal sealed class AutomaticPersistenceModule : IPersistenceModule
{
    public string Id { get; }
    public string Schema { get; }
    public PersistenceDatabaseTarget DatabaseTarget { get; }
    public IReadOnlyCollection<Type> EntityTypes { get; }

    public AutomaticPersistenceModule(string schema, PersistenceDatabaseTarget target, IReadOnlyCollection<Type> entityTypes)
    {
        // A schema can contain trailing/repeated underscores that module identifiers cannot.
        var schemaId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(schema)));
        Id = $"moongate.auto.{target.ToString().ToLowerInvariant()}.{schemaId}";
        Schema = schema;
        DatabaseTarget = target;
        EntityTypes = entityTypes;
    }
}
