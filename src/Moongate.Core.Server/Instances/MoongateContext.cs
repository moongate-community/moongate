using DryIoc;
using Moongate.Core.Server.Data.Configs.Runtime;
using Moongate.Core.Server.Extensions;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;

namespace Moongate.Core.Server.Instances;

public static class MoongateContext
{
    public static bool UseEventLoop { get; set; } = true;
    public static IContainer Container { get; set; }

    public static MoongateRuntimeConfig RuntimeConfig => Container.Resolve<MoongateRuntimeConfig>();

    public static IEventLoopService EventLoopService => Container.Resolve<IEventLoopService>();
    public static IEventBusService EventBusService => Container.Resolve<IEventBusService>();
    public static INetworkService NetworkService => Container.Resolve<INetworkService>();

    /// <summary>
    ///  Enqueues an action to be executed in the event loop with a specified priority.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="action"></param>
    /// <param name="priority"></param>
    /// <returns></returns>
    public static string EnqueueAction(string source, Action action, EventLoopPriority priority = EventLoopPriority.Normal)
    {
        if (UseEventLoop)
        {
            return EventLoopService.EnqueueAction(source, action, priority);
        }

        action.Invoke();

        return string.Empty;
    }
}
