using Moongate.Persistence.Data.Persistence;
using Moongate.Plugin.Abstractions.Interfaces;

namespace Moongate.Server.Data.Internal.Plugins;

/// <summary>
/// Registration-time plugin context backed by the bootstrap registration collector.
/// </summary>
internal sealed class MoongatePluginContext : IMoongatePluginContext
{
    private readonly PluginBootstrapRegistrations _registrations;

    public MoongatePluginContext(
        string pluginId,
        string pluginDirectory,
        PluginBootstrapRegistrations registrations
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        ArgumentNullException.ThrowIfNull(registrations);

        PluginId = pluginId;
        PluginDirectory = pluginDirectory;
        _registrations = registrations;
    }

    public string PluginId { get; }

    public string PluginDirectory { get; }

    public void RegisterConsoleCommand<TCommand>() where TCommand : class
        => _registrations.AddConsoleCommand(typeof(TCommand));

    public void RegisterFileLoader<TLoader>() where TLoader : class
        => _registrations.AddFileLoader(typeof(TLoader));

    public void RegisterGameEventListener<TListener>() where TListener : class
        => _registrations.AddGameEventListener(typeof(TListener));

    public void RegisterLuaUserData<TUserData>()
        => _registrations.AddLuaUserData(typeof(TUserData));

    public void RegisterPacketHandler<THandler>() where THandler : class
        => _registrations.AddPacketHandler(typeof(THandler));

    public void RegisterPersistenceDescriptor<TEntity, TKey>(PersistenceEntityDescriptor<TEntity, TKey> descriptor)
        where TKey : notnull
        => _registrations.AddPersistenceDescriptorRegistration(registry => registry.Register<TEntity, TKey>(descriptor));

    public void RegisterScriptModule<TScriptModule>() where TScriptModule : class
        => _registrations.AddScriptModule(typeof(TScriptModule));

    public void RegisterService<TService, TImplementation>(int priority = 0)
        where TService : class
        where TImplementation : class, TService
        => _registrations.AddServiceRegistration(typeof(TService), typeof(TImplementation), priority);
}
