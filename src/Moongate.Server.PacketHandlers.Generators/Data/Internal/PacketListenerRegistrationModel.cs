namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class PacketListenerRegistrationModel : IEquatable<PacketListenerRegistrationModel>
{
    public string ListenerTypeName { get; }

    public byte OpCode { get; }

    public PacketListenerRegistrationModel(string listenerTypeName, byte opCode)
    {
        ListenerTypeName = listenerTypeName;
        OpCode = opCode;
    }

    public bool Equals(PacketListenerRegistrationModel? other)
        => other is not null &&
           OpCode == other.OpCode &&
           string.Equals(ListenerTypeName, other.ListenerTypeName, StringComparison.Ordinal);

    public override bool Equals(object? obj)
        => obj is PacketListenerRegistrationModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((ListenerTypeName != null ? ListenerTypeName.GetHashCode() : 0) * 397) ^ OpCode.GetHashCode();
        }
    }
}
