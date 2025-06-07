using System.Runtime.CompilerServices;
using Moongate.Core.Extensions;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Types;
using NanoidDotNet;

namespace Moongate.Core.Server.Extensions;

public static class EventLoopServiceExtensions
{
    public static string EnqueueAction(
        Action action, EventLoopPriority priority = EventLoopPriority.Normal, [CallerMemberName] string actionName = ""
    )
    {
        var id = actionName + "_" + Nanoid.Generate();
        id = id.ToSnakeCase();
        return MoongateContext.EventLoopService.EnqueueAction(id, action, priority);
    }

    public static string EnqueueHighPriorityAction(
        Action action, [CallerMemberName] string actionName = ""
    )
    {
        return EnqueueAction(action, EventLoopPriority.High, actionName);
    }

    public static string EnqueueLowPriorityAction(
        Action action, [CallerMemberName] string actionName = ""
    )
    {
        return EnqueueAction(action, EventLoopPriority.Low, actionName);
    }
}
