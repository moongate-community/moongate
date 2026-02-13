namespace Moongate.Core.Persistence.Interfaces.Entities;

public interface IEntityReader
{
    TEntity DeserializeEntity<TEntity>(byte[] data, Type entityType) where TEntity : class;
}
