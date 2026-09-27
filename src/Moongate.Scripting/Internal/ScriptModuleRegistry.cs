using System.Text.RegularExpressions;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;

namespace Moongate.Scripting.Internal;

internal sealed class ScriptModuleRegistry : IScriptModuleRegistry
{
    private static readonly Regex EventNamePattern = new("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

    private readonly List<Type> _modules = [];
    private readonly List<Type> _enums = [];
    private readonly List<ScriptEventRegistration> _events = [];

    public IReadOnlyList<Type> ModuleTypes => _modules;
    public IReadOnlyList<Type> EnumTypes => _enums;
    public IReadOnlyList<ScriptEventRegistration> EventRegistrations => _events;

    public void AddEnum(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"{enumType.FullName} is not an enum.", nameof(enumType));
        }

        if (!_enums.Contains(enumType))
        {
            _enums.Add(enumType);
        }
    }

    public void AddEvent(ScriptEventRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!EventNamePattern.IsMatch(registration.Name))
        {
            throw new ArgumentException(
                $"Script event name '{registration.Name}' must be snake_case (^[a-z][a-z0-9_]*$).",
                nameof(registration)
            );
        }

        if (_events.Any(existing => existing.Name == registration.Name))
        {
            throw new InvalidOperationException($"Script event '{registration.Name}' is already registered.");
        }

        if (_events.Any(existing => existing.EventType == registration.EventType))
        {
            throw new InvalidOperationException(
                $"Event type {registration.EventType.FullName} is already published to Lua."
            );
        }

        _events.Add(registration);
    }

    public void AddModule(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        if (moduleType.GetCustomAttributes(typeof(ScriptModuleAttribute), false).Length == 0)
        {
            throw new ArgumentException($"{moduleType.FullName} carries no [ScriptModule].", nameof(moduleType));
        }

        if (_modules.Contains(moduleType))
        {
            throw new InvalidOperationException($"Script module {moduleType.FullName} is already registered.");
        }

        _modules.Add(moduleType);
    }
}
