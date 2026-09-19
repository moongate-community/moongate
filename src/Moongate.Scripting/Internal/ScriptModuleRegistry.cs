using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;

namespace Moongate.Scripting.Internal;

internal sealed class ScriptModuleRegistry : IScriptModuleRegistry
{
    private readonly List<Type> _modules = [];
    private readonly List<Type> _enums = [];

    public IReadOnlyList<Type> ModuleTypes => _modules;
    public IReadOnlyList<Type> EnumTypes => _enums;

    public void AddModule(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        if (moduleType.GetCustomAttributes(typeof(ScriptModuleAttribute), inherit: false).Length == 0)
        {
            throw new ArgumentException($"{moduleType.FullName} carries no [ScriptModule].", nameof(moduleType));
        }

        if (_modules.Contains(moduleType))
        {
            throw new InvalidOperationException($"Script module {moduleType.FullName} is already registered.");
        }

        _modules.Add(moduleType);
    }

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
}
