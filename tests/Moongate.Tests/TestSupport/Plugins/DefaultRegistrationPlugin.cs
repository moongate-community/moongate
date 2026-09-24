using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Tests.TestSupport.Services;
using Moongate.Tests.TestSupport.Services.Interfaces;

namespace Moongate.Tests.TestSupport.Plugins;

public sealed class DefaultRegistrationPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; }

    public DefaultRegistrationPlugin()
    {
        Metadata = new("default", "Default plugin", new(1, 0, 0));
    }

    public void Register(Container container)
    {
        container.RegisterInstance(new RegistrationDependency());
        container.AddMoongateService<IRegistrationService, RegistrationService>();
    }
}
