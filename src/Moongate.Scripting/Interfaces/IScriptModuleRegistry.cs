using Moongate.Scripting.Data.Scripts;

namespace Moongate.Scripting.Interfaces;

/// <summary>
///     The module and enum types the binder publishes at startup, in registration order.
/// </summary>
public interface IScriptModuleRegistry
{
    /// <summary>
    ///     Gets the classes carrying
    ///     <c>
    ///         [ScriptModule]
    ///     </c>
    ///     .
    /// </summary>
    IReadOnlyList<Type> ModuleTypes { get; }

    /// <summary>
    ///     Gets the enums published as global tables regardless of whether a signature uses them.
    /// </summary>
    IReadOnlyList<Type> EnumTypes { get; }

    /// <summary>
    ///     Gets the bus events published to Lua, in registration order.
    /// </summary>
    IReadOnlyList<ScriptEventRegistration> EventRegistrations { get; }

    /// <summary>
    ///     Adds an enum type. Adding the same type twice is ignored.
    /// </summary>
    void AddEnum(Type enumType);

    /// <summary>
    ///     Adds a bus event for Lua. A name that is not snake_case throws <see cref="ArgumentException" />; a name or
    ///     event type already registered throws <see cref="InvalidOperationException" />.
    /// </summary>
    void AddEvent(ScriptEventRegistration registration);

    /// <summary>
    ///     Adds a module type. Adding the same type twice throws.
    /// </summary>
    void AddModule(Type moduleType);
}
