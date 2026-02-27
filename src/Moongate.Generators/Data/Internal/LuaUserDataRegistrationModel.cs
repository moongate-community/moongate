namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class LuaUserDataRegistrationModel : IEquatable<LuaUserDataRegistrationModel>
{
    public string TypeName { get; }

    public LuaUserDataRegistrationModel(string typeName)
    {
        TypeName = typeName;
    }

    public bool Equals(LuaUserDataRegistrationModel? other)
        => other is not null && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal);

    public override bool Equals(object? obj)
        => obj is LuaUserDataRegistrationModel other && Equals(other);

    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(TypeName);
}
