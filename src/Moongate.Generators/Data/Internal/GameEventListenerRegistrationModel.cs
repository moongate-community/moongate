namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class GameEventListenerRegistrationModel : IEquatable<GameEventListenerRegistrationModel>
{
    public GameEventListenerRegistrationModel(string listenerTypeName, string eventTypeName)
    {
        ListenerTypeName = listenerTypeName;
        EventTypeName = eventTypeName;
    }

    public string ListenerTypeName { get; }

    public string EventTypeName { get; }

    public bool Equals(GameEventListenerRegistrationModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return ListenerTypeName == other.ListenerTypeName && EventTypeName == other.EventTypeName;
    }

    public override bool Equals(object? obj)
        => obj is GameEventListenerRegistrationModel other && Equals(other);

    public override int GetHashCode()
        => (ListenerTypeName.GetHashCode() * 397) ^ EventTypeName.GetHashCode();
}
