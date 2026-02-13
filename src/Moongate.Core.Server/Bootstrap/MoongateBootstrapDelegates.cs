using DryIoc;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Interfaces.Services;

namespace Moongate.Core.Server.Bootstrap;

public class MoongateBootstrapDelegates
{
    public delegate void ConfigureServicesDelegate(IContainer container);

    public delegate void ConfigureScriptEngineDelegate(IScriptEngineService scriptEngine);

    public delegate void ConfigureNetworkServicesDelegate(INetworkService networkService);

    public delegate void ShutdownRequestDelegate();

    public delegate void AfterInitializeDelegate(IContainer container, MoongateServerConfig serverConfig);
}
