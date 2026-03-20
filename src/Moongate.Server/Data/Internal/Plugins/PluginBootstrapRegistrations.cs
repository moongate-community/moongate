using Moongate.Persistence.Interfaces.Persistence;

namespace Moongate.Server.Data.Internal.Plugins;

/// <summary>
/// Collects plugin-provided bootstrap registrations before the final wiring phase.
/// </summary>
internal sealed class PluginBootstrapRegistrations
{
    private readonly List<(Type ServiceType, Type ImplementationType, int Priority)> _serviceRegistrations = [];
    private readonly List<Type> _packetHandlerTypes = [];
    private readonly List<Type> _gameEventListenerTypes = [];
    private readonly List<Type> _consoleCommandTypes = [];
    private readonly List<Type> _fileLoaderTypes = [];
    private readonly List<Type> _luaUserDataTypes = [];
    private readonly List<Type> _scriptModuleTypes = [];
    private readonly List<Action<IPersistenceEntityRegistry>> _persistenceDescriptorRegistrations = [];

    public IReadOnlyList<(Type ServiceType, Type ImplementationType, int Priority)> ServiceRegistrations
        => _serviceRegistrations;

    public IReadOnlyList<Type> PacketHandlerTypes => _packetHandlerTypes;

    public IReadOnlyList<Type> GameEventListenerTypes => _gameEventListenerTypes;

    public IReadOnlyList<Type> ConsoleCommandTypes => _consoleCommandTypes;

    public IReadOnlyList<Type> FileLoaderTypes => _fileLoaderTypes;

    public IReadOnlyList<Type> LuaUserDataTypes => _luaUserDataTypes;

    public IReadOnlyList<Type> ScriptModuleTypes => _scriptModuleTypes;

    public IReadOnlyList<Action<IPersistenceEntityRegistry>> PersistenceDescriptorRegistrations
        => _persistenceDescriptorRegistrations;

    public void AddServiceRegistration(Type serviceType, Type implementationType, int priority)
    {
        if (_serviceRegistrations.Any(registration => registration.ServiceType == serviceType &&
                                                     registration.ImplementationType == implementationType))
        {
            return;
        }

        _serviceRegistrations.Add((serviceType, implementationType, priority));
    }

    public void AddPacketHandler(Type handlerType)
        => AddTypeOnce(_packetHandlerTypes, handlerType);

    public void AddGameEventListener(Type listenerType)
        => AddTypeOnce(_gameEventListenerTypes, listenerType);

    public void AddConsoleCommand(Type commandType)
        => AddTypeOnce(_consoleCommandTypes, commandType);

    public void AddFileLoader(Type loaderType)
        => AddTypeOnce(_fileLoaderTypes, loaderType);

    public void AddLuaUserData(Type userDataType)
        => AddTypeOnce(_luaUserDataTypes, userDataType);

    public void AddScriptModule(Type scriptModuleType)
        => AddTypeOnce(_scriptModuleTypes, scriptModuleType);

    public void AddPersistenceDescriptorRegistration(Action<IPersistenceEntityRegistry> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _persistenceDescriptorRegistrations.Add(registration);
    }

    private static void AddTypeOnce(ICollection<Type> registrations, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (registrations.Contains(type))
        {
            return;
        }

        registrations.Add(type);
    }
}
