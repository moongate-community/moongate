using Moongate.Core.Interfaces.Entities;

namespace Moongate.Persistence.Interfaces;

/// <summary>Provides sequential access within one database transaction callback.</summary>
/// <remarks>Facades expire when the callback ends. Failed operations poison the transaction even when caught.</remarks>
public interface IPersistenceTransaction
{
    /// <summary>Gets a facade owned by this transaction's database target.</summary>
    IDataAccess<T> GetDataAccess<T>() where T : class, IMoongateEntity;
}
