namespace Moongate.Scripting.Interfaces;

/// <summary>The module and enum types the binder publishes at startup, in registration order.</summary>
public interface IScriptModuleRegistry
{
    /// <summary>Gets the classes carrying <c>[ScriptModule]</c>.</summary>
    IReadOnlyList<Type> ModuleTypes { get; }

    /// <summary>Gets the enums published as global tables regardless of whether a signature uses them.</summary>
    IReadOnlyList<Type> EnumTypes { get; }

    /// <summary>Adds a module type. Adding the same type twice throws.</summary>
    void AddModule(Type moduleType);

    /// <summary>Adds an enum type. Adding the same type twice is ignored.</summary>
    void AddEnum(Type enumType);
}
