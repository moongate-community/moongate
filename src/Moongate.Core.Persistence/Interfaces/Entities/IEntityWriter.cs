namespace Moongate.Core.Persistence.Interfaces.Entities;

public interface IEntityWriter
{
    byte[] SerializeEntity<T>(T entity) where T : class;
}
