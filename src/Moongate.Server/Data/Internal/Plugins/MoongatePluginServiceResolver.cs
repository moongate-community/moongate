using DryIoc;
using Moongate.Plugin.Abstractions.Interfaces;

namespace Moongate.Server.Data.Internal.Plugins;

/// <summary>
/// DryIoc-backed runtime resolver exposed to plugin initialization code.
/// </summary>
internal sealed class MoongatePluginServiceResolver : IMoongatePluginServiceResolver
{
    private readonly IResolver _resolver;

    public MoongatePluginServiceResolver(IResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    public object Resolve(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return _resolver.Resolve(serviceType);
    }

    public TService Resolve<TService>() where TService : notnull
        => _resolver.Resolve<TService>();

    public bool TryResolve(Type serviceType, out object? service)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        service = _resolver.Resolve(serviceType, IfUnresolved.ReturnDefaultIfNotRegistered);

        return service is not null;
    }

    public bool TryResolve<TService>(out TService? service) where TService : class
    {
        service = _resolver.Resolve<TService>(IfUnresolved.ReturnDefaultIfNotRegistered);

        return service is not null;
    }
}
