using Moongate.Core.Extensions.Strings;
using Moongate.Core.Server.Interfaces.Services;
using Serilog;

namespace Moongate.Core.Server.Services;

public class EventDispatcherService : IEventDispatcherService
{
    private readonly Dictionary<string, List<Action<object?>>> _eventHandlers = new();

    private readonly ILogger _logger = Log.ForContext<EventDispatcherService>();

    public EventDispatcherService(IEventBusService eventBusService)
    {
        eventBusService.AllEventsObservable.Subscribe(OnEvent);
    }

    public void SubscribeToEvent(string eventName, Action<object?> eventHandler)
    {
        if (!_eventHandlers.TryGetValue(eventName, out var eventHandlers))
        {
            eventHandlers = new();
            _eventHandlers.Add(eventName, eventHandlers);
        }

        eventHandlers.Add(eventHandler);
    }

    public void UnsubscribeFromEvent(string eventName, Action<object?> eventHandler)
    {
        if (!_eventHandlers.TryGetValue(eventName, out var eventHandlers))
        {
            return;
        }

        eventHandlers.Remove(eventHandler);
    }

    private void DispatchEvent(string eventName, object? eventData = null)
    {
        _logger.Debug("Dispatching event {EventName}", eventName);

        if (!_eventHandlers.TryGetValue(eventName, out var eventHandler))
        {
            return;
        }

        foreach (var handler in eventHandler)
        {
            handler(eventData);
        }
    }

    private void OnEvent(object obj)
    {
        DispatchEvent(obj.GetType().Name.ToSnakeCase().Replace("_event", ""), obj);
    }
}
