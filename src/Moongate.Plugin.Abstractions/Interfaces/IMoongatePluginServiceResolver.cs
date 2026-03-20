namespace Moongate.Plugin.Abstractions.Interfaces;

/// <summary>
/// Resolves services for plugin runtime initialization.
/// </summary>
public interface IMoongatePluginServiceResolver
{
    /// <summary>
    /// Resolves a service by type.
    /// </summary>
    /// <param name="serviceType">The service type to resolve.</param>
    /// <returns>The resolved service instance.</returns>
    object Resolve(Type serviceType);

    /// <summary>
    /// Resolves a service by generic type parameter.
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <returns>The resolved service instance.</returns>
    TService Resolve<TService>()
        where TService : notnull;

    /// <summary>
    /// Attempts to resolve a service by type.
    /// </summary>
    /// <param name="serviceType">The service type to resolve.</param>
    /// <param name="service">The resolved service instance.</param>
    /// <returns><c>true</c> when the service was resolved.</returns>
    bool TryResolve(Type serviceType, out object? service);

    /// <summary>
    /// Attempts to resolve a service by generic type parameter.
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <param name="service">The resolved service instance.</param>
    /// <returns><c>true</c> when the service was resolved.</returns>
    bool TryResolve<TService>(out TService? service)
        where TService : class;
}
