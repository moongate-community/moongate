using DryIoc;
using Moongate.Api.Registry;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Extensions;

namespace Moongate.Tests.TestSupport.Api;

internal sealed class ApiRegistrationPluginLoader : IPluginLoaderService
{
    private readonly Container _container;

    public IReadOnlyList<MoongatePluginData> Plugins => [];

    public ApiRegistrationPluginLoader(Container container)
    {
        _container = container;
    }

    public void LoadPlugins()
    {
        Assert.False(_container.Resolve<ApiRegistry>().IsFrozen);
        _container.RegisterApiHandler<IncrementHandler>();
    }
}
