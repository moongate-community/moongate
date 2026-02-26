namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class ScriptModuleRegistrationModel : IEquatable<ScriptModuleRegistrationModel>
{
    public string ModuleTypeName { get; }

    public ScriptModuleRegistrationModel(string moduleTypeName)
        => ModuleTypeName = moduleTypeName;

    public bool Equals(ScriptModuleRegistrationModel? other)
        => other is not null && string.Equals(ModuleTypeName, other.ModuleTypeName, StringComparison.Ordinal);

    public override bool Equals(object? obj)
        => obj is ScriptModuleRegistrationModel other && Equals(other);

    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(ModuleTypeName);
}
