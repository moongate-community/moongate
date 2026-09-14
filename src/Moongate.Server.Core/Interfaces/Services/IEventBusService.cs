using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Exposes the shared Moongate event bus to injectable server services.</summary>
public interface IEventBusService : IMoongateService, IMoongateEventBus
{
}
