namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class GameEventListenerRegistrationModel : IEquatable<GameEventListenerRegistrationModel>
{
    public GameEventListenerRegistrationModel(
        string listenerTypeName,
        string eventTypeName,
        int priority,
        bool implementsMoongateService
    )
    {
        ListenerTypeName = listenerTypeName;
        EventTypeName = eventTypeName;
        Priority = priority;
        ImplementsMoongateService = implementsMoongateService;
    }

    public string ListenerTypeName { get; }

    public string EventTypeName { get; }

    public int Priority { get; }

    public bool ImplementsMoongateService { get; }

    public bool Equals(GameEventListenerRegistrationModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return ListenerTypeName == other.ListenerTypeName
               && EventTypeName == other.EventTypeName
               && Priority == other.Priority
               && ImplementsMoongateService == other.ImplementsMoongateService;
    }

    public override bool Equals(object? obj)
        => obj is GameEventListenerRegistrationModel other && Equals(other);

    public override int GetHashCode()
    {
        var hash = (ListenerTypeName.GetHashCode() * 397) ^ EventTypeName.GetHashCode();
        hash = (hash * 397) ^ Priority;
        hash = (hash * 397) ^ ImplementsMoongateService.GetHashCode();

        return hash;
    }
}
