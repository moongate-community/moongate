namespace Moongate.Stress.Interfaces;

public interface IAccountBootstrapper
{
    Task EnsureUsersAsync(CancellationToken cancellationToken = default);
}
