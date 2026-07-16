namespace Moongate.Server.Interfaces.Events;

/// <summary>Wires the registered event subscribers to the bus at startup.</summary>
public interface IEventSubscriberService
{
    /// <summary>How many subscribers were wired.</summary>
    int Count { get; }
}
