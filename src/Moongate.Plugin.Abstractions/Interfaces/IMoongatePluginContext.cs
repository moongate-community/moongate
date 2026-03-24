using Moongate.Persistence.Data.Persistence;

namespace Moongate.Plugin.Abstractions.Interfaces;

/// <summary>
/// Defines the registration-time context exposed to Moongate plugins.
/// </summary>
public interface IMoongatePluginContext
{
    /// <summary>
    /// Gets the plugin id.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Gets the absolute plugin directory.
    /// </summary>
    string PluginDirectory { get; }

    /// <summary>
    /// Registers a console command type.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    void RegisterConsoleCommand<TCommand>() where TCommand : class;

    /// <summary>
    /// Registers a file loader type.
    /// </summary>
    /// <typeparam name="TLoader">The file loader type.</typeparam>
    void RegisterFileLoader<TLoader>() where TLoader : class;

    /// <summary>
    /// Registers a game event listener type.
    /// </summary>
    /// <typeparam name="TListener">The listener type.</typeparam>
    void RegisterGameEventListener<TListener>() where TListener : class;

    /// <summary>
    /// Registers a Lua user-data type.
    /// </summary>
    /// <typeparam name="TUserData">The user-data type.</typeparam>
    void RegisterLuaUserData<TUserData>();

    /// <summary>
    /// Registers a packet handler type.
    /// </summary>
    /// <typeparam name="THandler">The packet handler type.</typeparam>
    void RegisterPacketHandler<THandler>() where THandler : class;

    /// <summary>
    /// Registers a persistence descriptor.
    /// </summary>
    /// <typeparam name="TEntity">The persisted entity type.</typeparam>
    /// <typeparam name="TKey">The entity key type.</typeparam>
    /// <param name="descriptor">The descriptor to register.</param>
    void RegisterPersistenceDescriptor<TEntity, TKey>(PersistenceEntityDescriptor<TEntity, TKey> descriptor)
        where TKey : notnull;

    /// <summary>
    /// Registers a Lua script module type.
    /// </summary>
    /// <typeparam name="TScriptModule">The script module type.</typeparam>
    void RegisterScriptModule<TScriptModule>() where TScriptModule : class;

    /// <summary>
    /// Registers a service implementation for a service contract.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="priority">Optional startup priority for Moongate services.</param>
    void RegisterService<TService, TImplementation>(int priority = 0)
        where TService : class
        where TImplementation : class, TService;
}
