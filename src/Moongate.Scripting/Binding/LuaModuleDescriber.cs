using System.Reflection;
using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Data.Binding;
using Moongate.Scripting.Internal;

namespace Moongate.Scripting.Binding;

/// <summary>
///     Reads a [ScriptModule] class from its type alone: its functions and constants, checked as binding checks them.
///     The binder publishes what this describes, and the documentation lists it without building the module.
/// </summary>
public static class LuaModuleDescriber
{
    private const BindingFlags DeclaredMembers = BindingFlags.Public |
                                                 BindingFlags.NonPublic |
                                                 BindingFlags.Static |
                                                 BindingFlags.Instance |
                                                 BindingFlags.DeclaredOnly;

    /// <summary>
    ///     Reflects <paramref name="moduleType" /> once and returns its Lua name, functions and constants.
    /// </summary>
    /// <param name="moduleType">
    ///     The [ScriptModule]-attributed class.
    /// </param>
    /// <returns>
    ///     The module as Lua will see it; no instance is created.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     <paramref name="moduleType" /> carries no [ScriptModule], a [ScriptFunction] sits on a static, non-public or
    ///     generic method, two members resolve to the same Lua name, a signature uses a parameter or return type the
    ///     converter cannot bind, or a [ScriptConstant] is not a supported public static read-only member.
    /// </exception>
    public static ModuleDescription Describe(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        var moduleAttribute = moduleType.GetCustomAttribute<ScriptModuleAttribute>(false) ??
                              throw new InvalidOperationException($"{moduleType.FullName} carries no [ScriptModule].");
        var functions = new List<BoundFunction>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in moduleType.GetMethods(DeclaredMembers))
        {
            var attribute = method.GetCustomAttribute<ScriptFunctionAttribute>(false);

            if (attribute is null)
            {
                continue;
            }

            if (!method.IsPublic || method.IsStatic || method.IsGenericMethodDefinition)
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{method.Name}: a [ScriptFunction] must be a public, non-generic instance method."
                );
            }

            var luaName = attribute.Name ?? LuaModuleBinder.ToSnakeCase(method.Name);

            if (!seen.Add(luaName))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{method.Name}: Lua name '{luaName}' is already used in module '{moduleAttribute.Name}'."
                );
            }

            ValidateSignature(moduleType, method);
            functions.Add(new(luaName, method, attribute.HelpText));
        }

        var constants = DescribeConstants(moduleType, moduleAttribute.Name, seen);

        return new(moduleAttribute.Name, moduleAttribute.HelpText, moduleType, functions, constants);
    }

    /// <summary>
    ///     Lists the enum types a module mentions: the parameters and the return of each function, then the constants.
    /// </summary>
    /// <param name="module">
    ///     The described module.
    /// </param>
    /// <returns>
    ///     Each enum type once, in the order it is first met.
    /// </returns>
    public static IReadOnlyList<Type> EnumsOf(ModuleDescription module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var enums = new List<Type>();

        void Note(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying.IsEnum && !enums.Contains(underlying))
            {
                enums.Add(underlying);
            }
        }

        foreach (var function in module.Functions)
        {
            foreach (var parameter in function.Method.GetParameters())
            {
                var isParams = parameter.GetCustomAttribute<ParamArrayAttribute>() is not null;
                Note(isParams ? parameter.ParameterType.GetElementType()! : parameter.ParameterType);
            }

            Note(function.Method.ReturnType);
        }

        foreach (var constant in module.Constants)
        {
            Note(constant.Type);
        }

        return enums;
    }

    private static List<BoundConstant> DescribeConstants(Type moduleType, string moduleName, HashSet<string> seen)
    {
        var constants = new List<BoundConstant>();

        foreach (var member in moduleType.GetMembers(DeclaredMembers))
        {
            var attribute = member.GetCustomAttribute<ScriptConstantAttribute>(false);

            if (attribute is null)
            {
                continue;
            }

            var (type, isStaticReadOnly) = member switch
            {
                FieldInfo field => (field.FieldType, field.IsStatic && field.IsInitOnly && field.IsPublic),
                PropertyInfo property => (property.PropertyType,
                    property.GetMethod is { IsStatic: true, IsPublic: true } &&
                    property.SetMethod is null),
                _ => (typeof(void), false)
            };

            if (!isStaticReadOnly)
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{member.Name}: a [ScriptConstant] must be a public static readonly field or a public static get-only property."
                );
            }

            if (type == typeof(LuaTable) ||
                type == typeof(LuaValue) ||
                type == typeof(object) ||
                !LuaValueConverter.IsSupported(type))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{member.Name}: constants of type {type.Name} are not supported; use int, long, double, bool, string or an enum."
                );
            }

            var luaName = attribute.Name ?? member.Name;

            if (!seen.Add(luaName))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{member.Name}: Lua name '{luaName}' is already used in module '{moduleName}'."
                );
            }

            constants.Add(new(luaName, type, ReadConstant(moduleType, member), attribute.HelpText));
        }

        return constants;
    }

    /// <summary>
    ///     Reads a validated constant; a getter that throws becomes a binding error naming the member, with the getter's
    ///     exception as the cause.
    /// </summary>
    private static object? ReadConstant(Type moduleType, MemberInfo member)
    {
        try
        {
            return member switch
            {
                FieldInfo field       => field.GetValue(null),
                PropertyInfo property => property.GetValue(null),
                _                     => null
            };
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"{moduleType.FullName}.{member.Name}: the constant's getter threw {exception.InnerException.GetType().Name}: {exception.InnerException.Message}",
                exception.InnerException
            );
        }
    }

    private static void ValidateSignature(Type moduleType, MethodInfo method)
    {
        var parameters = method.GetParameters();

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var isParams = i == parameters.Length - 1 && parameter.GetCustomAttribute<ParamArrayAttribute>() is not null;
            var type = isParams ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (!LuaValueConverter.IsSupported(type) || type == typeof(void))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{method.Name}: parameter '{parameter.Name}' of type {parameter.ParameterType.Name} cannot be bound."
                );
            }
        }

        var returnType = Nullable.GetUnderlyingType(method.ReturnType) ?? method.ReturnType;

        if (!LuaValueConverter.IsSupported(returnType))
        {
            throw new InvalidOperationException(
                $"{moduleType.FullName}.{method.Name}: return type {method.ReturnType.Name} cannot be bound."
            );
        }
    }
}
