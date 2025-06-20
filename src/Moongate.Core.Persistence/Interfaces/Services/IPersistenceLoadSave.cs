namespace Moongate.Core.Persistence.Interfaces.Services;

public interface IPersistenceLoadSave
{
    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

}
